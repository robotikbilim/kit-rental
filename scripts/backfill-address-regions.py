#!/usr/bin/env python3
"""Match legacy address regions to Kargonomi IDs; audit by default, apply explicitly.

Credentials are read from the running Core container and never written to artifacts.
The output directory contains private address backups and must remain restricted.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import time
import unicodedata
import urllib.error
import urllib.request
import uuid

STORES = [
    ("CustomerAddresses", "Line1", ""),
    ("RentalOrders", "DeliveryLine1", "Delivery"),
    ("RentalCohortStudents", "AddressLine", ""),
    ("KitLocationEvents", "AddressLine", ""),
    ("FaultTickets", "ReporterAddress", ""),
    ("FaultKargonomiShipments", "RecipientAddress", ""),
    ("KitReturnRequests", "ReturnAddress", ""),
]


def normalize(value):
    text = unicodedata.normalize("NFKD", value.casefold().replace("ı", "i"))
    text = "".join(c for c in text if not unicodedata.combining(c))
    return " ".join(re.findall(r"[a-z0-9]+", text))


def runtime(container):
    inspected = json.loads(subprocess.check_output(["docker", "inspect", container], text=True))[0]
    settings = dict(item.split("=", 1) for item in inspected["Config"]["Env"] if "=" in item)
    connection = dict(part.split("=", 1) for part in settings["ConnectionStrings__CoreDatabase"].split(";") if "=" in part)
    return settings, {key.strip().lower(): value for key, value in connection.items()}


def sql(connection, query, json_output=False):
    environment = os.environ.copy()
    environment["SQLCMDPASSWORD"] = connection["password"]
    args = ["docker", "exec", "-i", "-e", "SQLCMDPASSWORD", "mssql_tools",
            "/opt/mssql-tools/bin/sqlcmd", "-S", connection["server"], "-U", connection["user id"],
            "-d", connection["database"], "-b", "-C", "-I", "-x", "-i", "/dev/stdin"]
    args += ["-y", "0", "-w", "65535"] if json_output else ["-W", "-h", "-1", "-w", "1000"]
    result = subprocess.run(args, input="SET NOCOUNT ON;\n" + query + "\nGO\n", text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=environment)
    if result.returncode:
        # SQL errors may embed address text. Expose diagnostics only as a private
        # exception attribute so callers can write them to a restricted artifact.
        error = RuntimeError("SQL operation failed; no address data was printed.")
        error.private_diagnostics = result.stdout + result.stderr
        raise error
    if not json_output:
        return result.stdout.strip()
    start, end = result.stdout.find("[{"), result.stdout.rfind("}]")
    if start < 0:
        if "[]" in result.stdout:
            return []
        raise RuntimeError("SQL JSON result could not be read.")
    # SQL Server emits FOR JSON in multiple rows; sqlcmd inserts physical line
    # breaks between those chunks. Address line breaks remain JSON-escaped.
    # Older sqlcmd can also insert a NUL at chunk boundaries. Real address
    # control characters are JSON-escaped, so these transport characters can
    # be removed without editing addresses. read_rows verifies SQL hashes.
    return json.loads(result.stdout[start:end + 2].replace("\r", "").replace("\n", "").replace("\x00", ""))


def provider_get(settings, path):
    base = settings.get("Kargonomi__BaseUrl", "https://app.kargonomi.com.tr/api/v1")
    request = urllib.request.Request(base.rstrip("/") + "/" + path,
        headers={"Accept": "application/json", "User-Agent": "Mozilla/5.0",
                 "Authorization": "Bearer " + settings["Kargonomi__ApiToken"]})
    for attempt in range(4):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                payload = json.load(response)
            items = payload if isinstance(payload, list) else payload.get("data")
            if not isinstance(items, list):
                raise RuntimeError("Kargonomi region list has an unexpected format.")
            return [{"id": int(item["id"]), "name": item.get("name", item.get("title", ""))} for item in items]
        except urllib.error.HTTPError as error:
            if error.code != 429 or attempt == 3:
                raise RuntimeError("Kargonomi catalog request failed: HTTP " + str(error.code)) from None
            delay = error.headers.get("Retry-After", "10")
            time.sleep(min(30, max(1, int(delay) if delay.isdigit() else 10)))


def catalog(settings, directory):
    path = directory / "kargonomi-regions.json"
    if path.exists():
        return json.loads(path.read_text())
    states = provider_get(settings, "states/1")
    districts = {}
    for index, state in enumerate(states):
        districts[str(state["id"])] = provider_get(settings, "cities/" + str(state["id"]))
        if (index + 1) % 10 == 0:
            print(json.dumps({"catalog_provinces_loaded": index + 1}), flush=True)
    result = {"states": states, "districts": districts}
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2))
    return result


def match(address, regions):
    # Remove only a verified leading region pair, never a slash inside building/floor text.
    prefix = re.match(r"^\s*([^/]+?)\s*/\s*([^/]+?)\s*-\s*(.+)$", address, flags=re.DOTALL)
    if prefix:
        states = [s for s in regions["states"] if normalize(s["name"]) == normalize(prefix[1])]
        if len(states) == 1:
            cities = [c for c in regions["districts"][str(states[0]["id"]) ]
                      if normalize(c["name"]) == normalize(prefix[2])]
            street = prefix[3].strip()
            if len(cities) == 1:
                return (states[0], cities[0], street) if street else None
    # A separated trailing district/province pair is also unambiguous. Do not search
    # arbitrary street mentions: a street named after a province is not evidence.
    tokens = [normalize(part) for part in re.split(r"[,;/\n]+", address) if normalize(part)]
    while tokens and (tokens[-1] in {"turkiye", "turkey", "tr"} or tokens[-1].isdigit()):
        tokens.pop()
    if len(tokens) >= 2:
        for state in regions["states"]:
            if tokens[-1] != normalize(state["name"]):
                continue
            cities = [c for c in regions["districts"][str(state["id"])] if tokens[-2] == normalize(c["name"])]
            if len(cities) == 1:
                # Preserve natural street text; only the dedicated canonical prefix
                # is removed when requested during activation of the new code.
                return state, cities[0], address
    # Historic imports also used a final "district province" (or reversed)
    # pair without punctuation. Match complete terminal names against the
    # provider catalog; names elsewhere in the street are insufficient.
    ending = re.sub(r"(\s+(?:turkiye|turkey|tr|\d{5}))+\s*$", "", normalize(address))
    candidates = []
    for state in regions["states"]:
        state_name = normalize(state["name"])
        for city in regions["districts"][str(state["id"])]:
            city_name = normalize(city["name"])
            if ending == city_name + " " + state_name or ending.endswith(" " + city_name + " " + state_name) or \
               ending == state_name + " " + city_name or ending.endswith(" " + state_name + " " + city_name):
                candidates.append((state, city, address))
    if len(candidates) == 1:
        return candidates[0]
    return None


def literal(value):
    return "NULL" if value is None else "N'" + str(value).replace("'", "''") + "'"


def read_rows(connection):
    rows, schemas = [], {}
    for table, address_column, prefix in STORES:
        columns = sql(connection, "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(" + literal(table) + ") FOR JSON PATH;", True)
        columns = {column["name"] for column in columns}
        if address_column not in columns:
            raise RuntimeError("Unexpected address column for " + table + ": " + address_column)
        schemas[table] = columns
        region_columns = [("cityId", prefix + "CityId"), ("districtId", prefix + "DistrictId"),
                          ("city", prefix + "City"), ("district", prefix + "District")]
        select = ["Id AS id", "[" + address_column + "] AS address",
                  "CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), [" + address_column + "])), 2) AS addressHash"]
        select += [("[" + column + "]" if column in columns else "NULL") + " AS [" + alias + "]"
                   for alias, column in region_columns]
        data = sql(connection, "SELECT " + ", ".join(select) + " FROM [" + table + "] FOR JSON PATH, INCLUDE_NULL_VALUES;", True)
        for row in data:
            if row["address"] is not None and hashlib.sha256(row["address"].encode("utf-16-le")).hexdigest().upper() != row["addressHash"]:
                raise RuntimeError("Address transport hash mismatch in " + table + "; no data will be updated.")
        rows.extend(dict(row, table=table, addressColumn=address_column, prefix=prefix) for row in data)
    return rows, schemas


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True)
    parser.add_argument("--container", default="kit_rental_core")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--strip-prefix", action="store_true", help="Use only when the structured-address code is activated.")
    args = parser.parse_args()
    os.umask(0o077)
    directory = Path(args.output)
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    directory.chmod(0o700)
    settings, connection = runtime(args.container)
    regions = catalog(settings, directory)
    rows, schemas = read_rows(connection)
    backup = directory / ("address-backup-" + str(time.time_ns()) + ".json")
    backup.write_text(json.dumps(rows, ensure_ascii=False, indent=2))
    plan, summaries = [], {}
    for row in rows:
        summary = summaries.setdefault(row["table"], {"rows": 0, "matched": 0, "unresolved": 0,
                                                      "already_set": 0, "blank": 0, "conflicts": 0})
        summary["rows"] += 1
        address = row["address"] or ""
        if not address.strip():
            summary["blank"] += 1
            continue
        resolved = match(address, regions) if address else None
        if resolved and ((row["cityId"] is not None and row["cityId"] != resolved[0]["id"]) or
                         (row["districtId"] is not None and row["districtId"] != resolved[1]["id"])):
            summary["conflicts"] += 1
            summary["unresolved"] += 1
            continue
        if row["cityId"] is not None and row["districtId"] is not None:
            summary["already_set"] += 1
            if not args.strip_prefix or not resolved or resolved[0]["id"] != row["cityId"] or resolved[1]["id"] != row["districtId"] or resolved[2] == address:
                continue
        elif resolved is None:
            summary["unresolved"] += 1
            continue
        summary["matched"] += 1
        state, city, street = resolved
        plan.append(dict(row, newCityId=state["id"], newDistrictId=city["id"], newCity=state["name"],
                         newDistrict=city["name"], newAddress=street if args.strip_prefix else address))
    (directory / "backfill-plan.json").write_text(json.dumps(plan, ensure_ascii=False, indent=2))
    if args.apply and plan:
        statements = ["SET XACT_ABORT ON; BEGIN TRANSACTION; DECLARE @updated int = 0;"]
        for row in plan:
            prefix = row["prefix"]
            required = {prefix + key for key in ["CityId", "DistrictId", "City", "District"]}
            if not required.issubset(schemas[row["table"]]):
                raise RuntimeError("Structured-address migration must be applied before backfill.")
            row_id = str(uuid.UUID(row["id"]))
            setters = [(prefix + "CityId", str(row["newCityId"])), (prefix + "DistrictId", str(row["newDistrictId"])),
                       (prefix + "City", literal(row["newCity"])), (prefix + "District", literal(row["newDistrict"])),
                       (row["addressColumn"], literal(row["newAddress"]))]
            guards = ["Id = " + literal(row_id), "CONVERT(varbinary(max), [" + row["addressColumn"] + "]) = CONVERT(varbinary(max), " + literal(row["address"]) + ")"]
            for key in ["cityId", "districtId"]:
                column = prefix + ("CityId" if key == "cityId" else "DistrictId")
                guards.append("[" + column + "] IS NULL" if row[key] is None else "[" + column + "] = " + str(int(row[key])))
            statements.append("UPDATE [" + row["table"] + "] SET " + ", ".join("[" + key + "] = " + value for key, value in setters)
                              + " WHERE " + " AND ".join(guards) + "; SET @updated += @@ROWCOUNT;")
        statements.append("IF @updated <> " + str(len(plan)) + " THROW 51000, 'Concurrent address change; backfill rolled back.', 1; COMMIT TRANSACTION; SELECT @updated AS UpdatedRows;")
        try:
            sql(connection, "\n".join(statements))
        except RuntimeError as error:
            diagnostics = getattr(error, "private_diagnostics", "")
            if diagnostics:
                diagnostic_path = directory / "sql-failure.private.txt"
                diagnostic_path.write_text(diagnostics)
                diagnostic_path.chmod(0o600)
            raise
    report = {"applied": args.apply, "strip_prefix": args.strip_prefix, "planned_updates": len(plan), "tables": summaries,
              "catalog_provinces": len(regions["states"]), "catalog_districts": sum(map(len, regions["districts"].values()))}
    (directory / "backfill-summary.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
    for artifact in directory.glob("*.json"):
        artifact.chmod(0o600)
    print(json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
