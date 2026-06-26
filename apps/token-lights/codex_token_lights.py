from __future__ import annotations

import argparse
import json
import os
import re
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any


GREEN_LIMIT = 80_000
RED_LIMIT = 150_000
WINDOW_YELLOW_LIMIT = 50.0
WINDOW_RED_LIMIT = 75.0
TAIL_BYTES = 6 * 1024 * 1024
MAX_LOG_FILES = 80
DEFAULT_LIMIT = 20

SESSION_ID_RE = re.compile(
    r"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\.jsonl$",
    re.IGNORECASE,
)


@dataclass
class ThreadName:
    name: str
    updated_at: float


@dataclass
class TokenRecord:
    thread_id: str
    input_tokens: int
    cached_input_tokens: int
    output_tokens: int
    total_tokens: int
    model_context_window: int | None
    event_time: float
    log_mtime: float
    log_path: Path
    recent_inputs: list[int]
    primary_remaining_percent: float | None
    primary_window_minutes: int | None
    primary_resets_at: int | None
    secondary_remaining_percent: float | None
    secondary_window_minutes: int | None
    secondary_resets_at: int | None

    @property
    def uncached_input_tokens(self) -> int:
        return max(0, self.input_tokens - self.cached_input_tokens)


@dataclass
class DisplayRow:
    thread_id: str
    name: str
    level: str
    input_tokens: int
    cached_input_tokens: int
    uncached_input_tokens: int
    model_context_window: int | None
    context_window_percent: float | None
    event_time: float
    primary_remaining_percent: float | None
    primary_window_minutes: int | None
    primary_resets_at: int | None
    secondary_remaining_percent: float | None
    secondary_window_minutes: int | None
    secondary_resets_at: int | None


class CodexTokenScanner:
    def __init__(self, codex_home: Path) -> None:
        self.codex_home = codex_home
        self._file_cache: dict[Path, tuple[int, int, TokenRecord | None]] = {}
        self._fallback_name_cache: dict[Path, tuple[int, str | None]] = {}

    def scan(self, limit: int = DEFAULT_LIMIT) -> list[DisplayRow]:
        names = self._read_thread_names()
        records = self._read_recent_token_records()
        rows: list[DisplayRow] = []

        for thread_id, record in records.items():
            fallback_name = self._read_fallback_name(record.log_path) or short_id(thread_id)
            name = names.get(thread_id, ThreadName(fallback_name, 0)).name
            display_name = name.strip() or short_id(thread_id)
            rows.append(self._build_row(record, display_name))

        rows.sort(key=lambda row: row.event_time, reverse=True)
        recent_rows = rows[:limit]
        recent_rows.sort(key=row_sort_key)
        return recent_rows

    def _read_thread_names(self) -> dict[str, ThreadName]:
        index_path = self.codex_home / "session_index.jsonl"
        names: dict[str, ThreadName] = {}
        if not index_path.exists():
            return names

        with index_path.open("r", encoding="utf-8-sig", errors="replace") as handle:
            for line in handle:
                try:
                    item = json.loads(line)
                except json.JSONDecodeError:
                    continue
                thread_id = str(item.get("id") or "")
                thread_name = str(item.get("thread_name") or "")
                if not thread_id or not thread_name:
                    continue
                updated_at = parse_timestamp(item.get("updated_at"))
                previous = names.get(thread_id)
                if previous is None or updated_at >= previous.updated_at:
                    names[thread_id] = ThreadName(thread_name, updated_at)
        return names

    def _read_recent_token_records(self) -> dict[str, TokenRecord]:
        records: dict[str, TokenRecord] = {}
        for path in self._recent_log_files():
            thread_id = extract_thread_id(path)
            if thread_id is None:
                continue
            record = self._read_token_record(path, thread_id)
            if record is None:
                continue
            previous = records.get(thread_id)
            if previous is None or record.event_time >= previous.event_time:
                records[thread_id] = record
        return records

    def _recent_log_files(self) -> list[Path]:
        session_root = self.codex_home / "sessions"
        if not session_root.exists():
            return []

        paths = [path for path in session_root.rglob("*.jsonl") if path.is_file()]
        paths.sort(key=lambda path: path.stat().st_mtime, reverse=True)
        return paths[:MAX_LOG_FILES]

    def _read_token_record(self, path: Path, thread_id: str) -> TokenRecord | None:
        stat = path.stat()
        cached = self._file_cache.get(path)
        if cached and cached[0] == stat.st_mtime_ns and cached[1] == stat.st_size:
            return cached[2]

        record = parse_latest_token_record(path, thread_id, stat.st_size, stat.st_mtime)
        self._file_cache[path] = (stat.st_mtime_ns, stat.st_size, record)
        return record

    def _read_fallback_name(self, path: Path) -> str | None:
        stat = path.stat()
        cached = self._fallback_name_cache.get(path)
        if cached and cached[0] == stat.st_mtime_ns:
            return cached[1]
        name = read_fallback_name_from_session_meta(path)
        self._fallback_name_cache[path] = (stat.st_mtime_ns, name)
        return name

    def _build_row(self, record: TokenRecord, name: str) -> DisplayRow:
        window_percent = context_window_percent(
            record.input_tokens, record.model_context_window
        )
        return DisplayRow(
            thread_id=record.thread_id,
            name=name,
            level=level_for(record.input_tokens, window_percent),
            input_tokens=record.input_tokens,
            cached_input_tokens=record.cached_input_tokens,
            uncached_input_tokens=record.uncached_input_tokens,
            model_context_window=record.model_context_window,
            context_window_percent=window_percent,
            event_time=record.event_time,
            primary_remaining_percent=record.primary_remaining_percent,
            primary_window_minutes=record.primary_window_minutes,
            primary_resets_at=record.primary_resets_at,
            secondary_remaining_percent=record.secondary_remaining_percent,
            secondary_window_minutes=record.secondary_window_minutes,
            secondary_resets_at=record.secondary_resets_at,
        )


def parse_latest_token_record(
    path: Path, thread_id: str, file_size: int, log_mtime: float
) -> TokenRecord | None:
    text = read_tail_text(path, file_size)
    latest_payload: dict[str, Any] | None = None
    latest_time = 0.0
    recent_inputs: list[int] = []

    for line in text.splitlines():
        if '"token_count"' not in line:
            continue
        try:
            item = json.loads(line)
        except json.JSONDecodeError:
            continue
        if item.get("type") != "event_msg":
            continue
        payload = item.get("payload") or {}
        if payload.get("type") != "token_count":
            continue
        info = payload.get("info") or {}
        usage = info.get("last_token_usage") or {}
        input_tokens = int_or_zero(usage.get("input_tokens"))
        if input_tokens <= 0:
            continue
        recent_inputs.append(input_tokens)
        latest_payload = payload
        latest_time = parse_timestamp(item.get("timestamp")) or log_mtime

    if latest_payload is None:
        return None

    info = latest_payload.get("info") or {}
    usage = info.get("last_token_usage") or {}
    rate_limits = latest_payload.get("rate_limits") or {}
    primary_rate = rate_limits.get("primary") or {}
    secondary_rate = rate_limits.get("secondary") or {}
    return TokenRecord(
        thread_id=thread_id,
        input_tokens=int_or_zero(usage.get("input_tokens")),
        cached_input_tokens=int_or_zero(usage.get("cached_input_tokens")),
        output_tokens=int_or_zero(usage.get("output_tokens")),
        total_tokens=int_or_zero(usage.get("total_tokens")),
        model_context_window=int_or_none(info.get("model_context_window")),
        event_time=latest_time,
        log_mtime=log_mtime,
        log_path=path,
        recent_inputs=recent_inputs[-3:],
        primary_remaining_percent=remaining_percent(primary_rate.get("used_percent")),
        primary_window_minutes=int_or_none(primary_rate.get("window_minutes")),
        primary_resets_at=int_or_none(primary_rate.get("resets_at")),
        secondary_remaining_percent=remaining_percent(secondary_rate.get("used_percent")),
        secondary_window_minutes=int_or_none(secondary_rate.get("window_minutes")),
        secondary_resets_at=int_or_none(secondary_rate.get("resets_at")),
    )


def read_tail_text(path: Path, file_size: int) -> str:
    with path.open("rb") as handle:
        start = max(0, file_size - TAIL_BYTES)
        handle.seek(start)
        data = handle.read()
    text = data.decode("utf-8", errors="replace")
    if start > 0:
        parts = text.split("\n", 1)
        return parts[1] if len(parts) == 2 else ""
    return text


def read_fallback_name_from_session_meta(path: Path) -> str | None:
    try:
        with path.open("rb") as handle:
            first_line = handle.readline(512 * 1024)
    except OSError:
        return None
    if not first_line:
        return None
    try:
        item = json.loads(first_line.decode("utf-8-sig", errors="replace"))
    except json.JSONDecodeError:
        return None
    if item.get("type") != "session_meta":
        return None
    payload = item.get("payload") or {}
    for key in ("thread_name", "name", "title"):
        value = payload.get(key)
        if value:
            return str(value)
    cwd = payload.get("cwd")
    if cwd:
        return Path(str(cwd)).name.replace("-", " ")
    return None


def extract_thread_id(path: Path) -> str | None:
    match = SESSION_ID_RE.search(path.name)
    return match.group(1) if match else None


def parse_timestamp(value: Any) -> float:
    if not value:
        return 0.0
    try:
        text = str(value).replace("Z", "+00:00")
        return datetime.fromisoformat(text).timestamp()
    except ValueError:
        return 0.0


def int_or_zero(value: Any) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return 0


def int_or_none(value: Any) -> int | None:
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def float_or_none(value: Any) -> float | None:
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def remaining_percent(used_percent: Any) -> float | None:
    used = float_or_none(used_percent)
    if used is None:
        return None
    return max(0.0, min(100.0, 100.0 - used))


def short_id(thread_id: str) -> str:
    return thread_id[:8] if thread_id else "unknown"


def context_window_percent(input_tokens: int, model_context_window: int | None) -> float | None:
    if not model_context_window or model_context_window <= 0:
        return None
    return max(0.0, (input_tokens / model_context_window) * 100.0)


def level_for(input_tokens: int, window_percent: float | None = None) -> str:
    if input_tokens >= RED_LIMIT or (
        window_percent is not None and window_percent >= WINDOW_RED_LIMIT
    ):
        return "red"
    if input_tokens >= GREEN_LIMIT or (
        window_percent is not None and window_percent >= WINDOW_YELLOW_LIMIT
    ):
        return "yellow"
    return "green"


def row_sort_key(row: DisplayRow) -> tuple[int, float]:
    severity = {"red": 0, "yellow": 1, "green": 2}.get(row.level, 3)
    return (severity, -row.event_time)


def format_tokens(value: int) -> str:
    if value >= 1_000_000:
        return f"{value / 1_000_000:.1f}m"
    if value >= 10_000:
        return f"{round(value / 1000):.0f}k"
    if value >= 1_000:
        return f"{value / 1000:.1f}k"
    return str(value)


def format_percent(value: float | None) -> str:
    if value is None:
        return "--"
    if abs(value - round(value)) < 0.05:
        return f"{value:.0f}%"
    return f"{value:.1f}%"


def format_window_percent(value: float | None) -> str:
    if value is None:
        return "--"
    return f"{value:.0f}%"


def format_reset_short(timestamp: int | None) -> str:
    if not timestamp:
        return "--"
    dt = datetime.fromtimestamp(timestamp)
    now = datetime.now()
    if dt.date() == now.date():
        return dt.strftime("%H:%M")
    return f"{dt.month}月{dt.day}日"


def default_codex_home() -> Path:
    env_home = os.environ.get("CODEX_HOME")
    if env_home:
        return Path(env_home).expanduser()
    return Path.home() / ".codex"


def truncate(text: str, width: int) -> str:
    if len(text) <= width:
        return text
    return text[: max(0, width - 3)] + "..."


def print_once(rows: list[DisplayRow]) -> None:
    for row in rows:
        lamp = {"green": "GREEN", "yellow": "YELLOW", "red": "RED"}[row.level]
        print(
            f"{lamp}  {truncate(row.name, 28):<28}  "
            f"{format_tokens(row.input_tokens):>7}  {format_window_percent(row.context_window_percent):>4}"
        )


def rows_to_json(rows: list[DisplayRow]) -> str:
    payload = [
        {
            "thread_id": row.thread_id,
            "name": row.name,
            "level": row.level,
            "input_tokens": row.input_tokens,
            "input_tokens_short": format_tokens(row.input_tokens),
            "cached_input_tokens": row.cached_input_tokens,
            "uncached_input_tokens": row.uncached_input_tokens,
            "model_context_window": row.model_context_window,
            "context_window_percent": row.context_window_percent,
            "context_window_short": format_window_percent(row.context_window_percent),
            "event_time": row.event_time,
            "primary_remaining_percent": row.primary_remaining_percent,
            "primary_remaining_short": format_percent(row.primary_remaining_percent),
            "primary_window_minutes": row.primary_window_minutes,
            "primary_resets_at": row.primary_resets_at,
            "primary_reset_short": format_reset_short(row.primary_resets_at),
            "secondary_remaining_percent": row.secondary_remaining_percent,
            "secondary_remaining_short": format_percent(row.secondary_remaining_percent),
            "secondary_window_minutes": row.secondary_window_minutes,
            "secondary_resets_at": row.secondary_resets_at,
            "secondary_reset_short": format_reset_short(row.secondary_resets_at),
        }
        for row in rows
    ]
    return json.dumps(payload, ensure_ascii=False)


def main() -> int:
    parser = argparse.ArgumentParser(description="Codex token traffic-light scanner.")
    parser.add_argument("--once", action="store_true", help="print one scan and exit")
    parser.add_argument("--json", action="store_true", help="print one scan as JSON and exit")
    parser.add_argument("--json-path", type=Path, help="write one JSON scan to this path and exit")
    parser.add_argument("--limit", type=int, default=DEFAULT_LIMIT, help="rows to include")
    parser.add_argument("--codex-home", type=Path, default=default_codex_home())
    args = parser.parse_args()

    scanner = CodexTokenScanner(args.codex_home)
    rows = scanner.scan(args.limit)

    if args.json_path:
        args.json_path.write_text(rows_to_json(rows), encoding="utf-8")
        return 0
    if args.json:
        print(rows_to_json(rows))
        return 0
    print_once(rows)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
