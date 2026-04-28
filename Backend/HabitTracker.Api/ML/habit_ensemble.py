import json
import pickle
import random
import sys
from pathlib import Path

import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import StratifiedKFold
from sklearn.preprocessing import StandardScaler
from xgboost import XGBClassifier

try:
    import torch
    from torch import nn
except Exception:
    torch = None
    nn = None


NUMERIC_FEATURES = [
    "IsWeekend",
    "TargetCount",
    "CurrentStreak",
    "Momentum",
    "FatigueScore",
    "StreakVolatility",
    "RecentSuccessRate",
    "UserOverallRate",
    "ContextDriftScore",
    "RecoverySpeed",
    "LogHabitAge",
    "SynergyScore",
    "IsHoliday",
    "SinDayOfYear",
    "CosDayOfYear",
    "RetroactiveLogRatio",
    "CueStrengthScore",
]

MATURITY_BUCKETS = ["Week1", "Week2", "Month1", "Month3", "Established"]
FEATURE_NAMES = NUMERIC_FEATURES + [f"MaturityBucket={bucket}" for bucket in MATURITY_BUCKETS]


class SimpleTabularNet(nn.Module):
    def __init__(self, input_size):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(input_size, 32),
            nn.ReLU(),
            nn.Dropout(0.10),
            nn.Linear(32, 16),
            nn.ReLU(),
            nn.Linear(16, 1),
        )

    def forward(self, x):
        return self.net(x).squeeze(1)


def _get(item, key, default=0.0):
    return item.get(key, item.get(key[:1].lower() + key[1:], default))


def vectorize(items):
    rows = []
    for item in items:
        row = [float(_get(item, name, 0.0) or 0.0) for name in NUMERIC_FEATURES]
        bucket = str(_get(item, "MaturityBucket", ""))
        row.extend(1.0 if bucket == known_bucket else 0.0 for known_bucket in MATURITY_BUCKETS)
        rows.append(row)
    return np.asarray(rows, dtype=np.float32)


def labels(items):
    return np.asarray([1 if bool(_get(item, "Label", False)) else 0 for item in items], dtype=np.int64)


def train_torch_nn(x, y):
    if torch is None:
        return None

    torch.manual_seed(1337)
    random.seed(1337)
    np.random.seed(1337)

    scaler = StandardScaler()
    x_scaled = scaler.fit_transform(x).astype(np.float32)

    model = SimpleTabularNet(x_scaled.shape[1])
    optimizer = torch.optim.Adam(model.parameters(), lr=0.01, weight_decay=0.0005)
    positives = max(int(y.sum()), 1)
    negatives = max(int(len(y) - y.sum()), 1)
    pos_weight = torch.tensor([negatives / positives], dtype=torch.float32)
    loss_fn = nn.BCEWithLogitsLoss(pos_weight=pos_weight)

    x_tensor = torch.tensor(x_scaled, dtype=torch.float32)
    y_tensor = torch.tensor(y, dtype=torch.float32)

    model.train()
    for _ in range(140):
        optimizer.zero_grad()
        loss = loss_fn(model(x_tensor), y_tensor)
        loss.backward()
        optimizer.step()

    return {
        "scaler": scaler,
        "state_dict": model.state_dict(),
        "input_size": x_scaled.shape[1],
    }


def predict_torch_nn(model_bundle, x):
    if torch is None or model_bundle is None:
        return None

    scaler = model_bundle["scaler"]
    x_scaled = scaler.transform(x).astype(np.float32)
    model = SimpleTabularNet(model_bundle["input_size"])
    model.load_state_dict(model_bundle["state_dict"])
    model.eval()

    with torch.no_grad():
        logits = model(torch.tensor(x_scaled, dtype=torch.float32))
        return torch.sigmoid(logits).numpy()


def train_xgboost(x, y):
    model = XGBClassifier(
        n_estimators=140,
        max_depth=3,
        learning_rate=0.06,
        subsample=0.9,
        colsample_bytree=0.9,
        eval_metric="logloss",
        random_state=1337,
        n_jobs=1,
    )
    model.fit(x, y)
    return model


def train_logistic(x, y):
    scaler = StandardScaler().fit(x)
    model = LogisticRegression(max_iter=500, class_weight="balanced", random_state=1337)
    model.fit(scaler.transform(x), y)
    return {"scaler": scaler, "model": model}


def predict_logistic(model_bundle, x):
    scaled = model_bundle["scaler"].transform(x)
    return model_bundle["model"].predict_proba(scaled)[:, 1]


def build_meta_features(models, x):
    columns = []
    names = []

    if "XGBoost" in models:
        columns.append(models["XGBoost"].predict_proba(x)[:, 1])
        names.append("XGBoost")

    if "LogisticRegression" in models:
        columns.append(predict_logistic(models["LogisticRegression"], x))
        names.append("LogisticRegression")

    if "TorchNN" in models:
        torch_probability = predict_torch_nn(models["TorchNN"], x)
        if torch_probability is not None:
            columns.append(torch_probability)
            names.append("TorchNN")

    if not columns:
        return np.empty((len(x), 0), dtype=np.float32), names

    return np.vstack(columns).T.astype(np.float32), names


def train_stacked_ensemble(x, y):
    class_counts = np.bincount(y)
    min_class_count = int(class_counts[class_counts > 0].min())
    n_splits = min(5, min_class_count)
    use_oof = n_splits >= 2 and len(y) >= 30

    model_names = ["XGBoost", "LogisticRegression"]
    if torch is not None:
        model_names.append("TorchNN")

    oof_predictions = np.zeros((len(y), len(model_names)), dtype=np.float32)

    if use_oof:
        splitter = StratifiedKFold(n_splits=n_splits, shuffle=True, random_state=1337)
        for train_index, valid_index in splitter.split(x, y):
            fold_models = {
                "XGBoost": train_xgboost(x[train_index], y[train_index]),
                "LogisticRegression": train_logistic(x[train_index], y[train_index]),
            }
            torch_nn = train_torch_nn(x[train_index], y[train_index])
            if torch_nn is not None:
                fold_models["TorchNN"] = torch_nn

            fold_meta, fold_names = build_meta_features(fold_models, x[valid_index])
            for column_index, name in enumerate(fold_names):
                target_index = model_names.index(name)
                oof_predictions[valid_index, target_index] = fold_meta[:, column_index]
    else:
        temp_models = {
            "XGBoost": train_xgboost(x, y),
            "LogisticRegression": train_logistic(x, y),
        }
        torch_nn = train_torch_nn(x, y)
        if torch_nn is not None:
            temp_models["TorchNN"] = torch_nn
        oof_predictions, model_names = build_meta_features(temp_models, x)

    final_models = {
        "XGBoost": train_xgboost(x, y),
        "LogisticRegression": train_logistic(x, y),
    }
    torch_nn = train_torch_nn(x, y)
    if torch_nn is not None:
        final_models["TorchNN"] = torch_nn

    final_meta, final_names = build_meta_features(final_models, x[:1])
    active_indexes = [model_names.index(name) for name in final_names]
    meta_train = oof_predictions[:, active_indexes]

    meta_model = LogisticRegression(max_iter=500, class_weight="balanced", random_state=1337)
    meta_model.fit(meta_train, y)

    return final_models, meta_model, final_names, use_oof


def xgboost_contributions(xgb_model, x):
    try:
        contributions = xgb_model.get_booster().predict(
            xgb_model.get_booster().DMatrix(x),
            pred_contribs=True,
        )
    except Exception:
        try:
            import xgboost as xgb

            contributions = xgb_model.get_booster().predict(xgb.DMatrix(x), pred_contribs=True)
        except Exception:
            return []

    row = contributions[0][:-1]
    ordered = sorted(
        (
            {
                "feature": FEATURE_NAMES[index] if index < len(FEATURE_NAMES) else f"feature_{index}",
                "value": float(value),
                "direction": "positive" if value >= 0 else "negative",
            }
            for index, value in enumerate(row)
        ),
        key=lambda item: abs(item["value"]),
        reverse=True,
    )
    return ordered[:5]


def train(training_path, model_path):
    with open(training_path, "r", encoding="utf-8-sig") as f:
        items = json.load(f)

    x = vectorize(items)
    y = labels(items)
    if len(y) < 20 or len(np.unique(y)) < 2:
        raise RuntimeError("Not enough diverse training data for ensemble")

    models, meta_model, meta_feature_names, used_oof = train_stacked_ensemble(x, y)

    Path(model_path).parent.mkdir(parents=True, exist_ok=True)
    with open(model_path, "wb") as f:
        pickle.dump(
            {
                "models": models,
                "meta_model": meta_model,
                "meta_feature_names": meta_feature_names,
                "used_oof": used_oof,
            },
            f,
        )

    onnx_path = try_export_onnx(models, meta_model, meta_feature_names, Path(model_path).with_suffix(".onnx"))
    print(json.dumps({"trained": list(models.keys()), "metaModel": "LogisticRegression", "usedOof": used_oof, "onnxPath": onnx_path}))


def try_export_onnx(models, meta_model, meta_feature_names, output_path):
    try:
        import skl2onnx
        from skl2onnx.common.data_types import FloatTensorType

        initial_type = [("meta_features", FloatTensorType([None, len(meta_feature_names)]))]
        onnx_model = skl2onnx.convert_sklearn(meta_model, initial_types=initial_type, target_opset=12)
        with open(output_path, "wb") as f:
            f.write(onnx_model.SerializeToString())
        return str(output_path)
    except Exception:
        return None


def predict_one(bundle, x):
    models = bundle.get("models", {})
    output = {}

    if "XGBoost" in models:
        output["XGBoost"] = float(models["XGBoost"].predict_proba(x)[0, 1])

    if "LogisticRegression" in models:
        pipeline = models["LogisticRegression"]
        scaled = pipeline["scaler"].transform(x)
        output["LogisticRegression"] = float(pipeline["model"].predict_proba(scaled)[0, 1])

    if "TorchNN" in models:
        torch_probability = predict_torch_nn(models["TorchNN"], x)
        if torch_probability is not None:
            output["TorchNN"] = float(torch_probability[0])

    meta_features, meta_names = build_meta_features(models, x)
    stacked_probability = None
    meta_model = bundle.get("meta_model")
    expected_names = bundle.get("meta_feature_names", meta_names)
    if meta_model is not None and meta_features.shape[1] > 0:
        name_to_column = {name: index for index, name in enumerate(meta_names)}
        ordered_columns = [
            meta_features[:, name_to_column[name]]
            for name in expected_names
            if name in name_to_column
        ]
        if len(ordered_columns) == len(expected_names):
            ordered_meta = np.vstack(ordered_columns).T.astype(np.float32)
            stacked_probability = float(meta_model.predict_proba(ordered_meta)[0, 1])

    return {
        "modelProbabilities": output,
        "stackedProbability": stacked_probability,
        "shapContributions": xgboost_contributions(models["XGBoost"], x) if "XGBoost" in models else [],
    }


def predict(model_path, input_path):
    with open(model_path, "rb") as f:
        bundle = pickle.load(f)

    with open(input_path, "r", encoding="utf-8-sig") as f:
        payload = json.load(f)

    if isinstance(payload, list):
        x = vectorize(payload)
        predictions = [predict_one(bundle, x[index : index + 1]) for index in range(len(payload))]
        print(json.dumps({"predictions": predictions}))
    else:
        x = vectorize([payload])
        print(json.dumps(predict_one(bundle, x)))


def main():
    if len(sys.argv) != 4:
        raise SystemExit("Usage: habit_ensemble.py train <training.json> <model.pkl> | predict <model.pkl> <input.json>")

    command = sys.argv[1]
    if command == "train":
        train(sys.argv[2], sys.argv[3])
    elif command == "predict":
        predict(sys.argv[2], sys.argv[3])
    else:
        raise SystemExit(f"Unknown command: {command}")


if __name__ == "__main__":
    main()
