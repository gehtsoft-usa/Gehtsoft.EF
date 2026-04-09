#!/usr/bin/env python3
"""Coverage report generator — summary and gap analysis.

Modes:
  summary  — Overall coverage + per-module breakdown
  gaps     — Top N least covered items (by function, type, namespace, or module)
"""

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict


# --- Mechanical method filters (same as parse_coverage.py) ---

MECHANICAL_NAME_PATTERNS = [
    re.compile(r'^get_'),
    re.compile(r'^set_'),
    re.compile(r'^\.c(?:c)?tor'),
    re.compile(r'^Dispose(?:\(|$)'),
    re.compile(r'^ToString(?:\(|$)'),
    re.compile(r'^GetHashCode(?:\(|$)'),
    re.compile(r'^Equals(?:\(|$)'),
    re.compile(r'^op_'),
    re.compile(r'^CompareTo(?:\(|$)'),
]

COMPILER_GENERATED_TYPE_PATTERNS = [
    re.compile(r'<>c__DisplayClass'),
    re.compile(r'<.*>d__'),
    re.compile(r'<.*>g__'),
    re.compile(r'\$'),
]

TEST_MODULE_PATTERNS = [
    re.compile(r'\.Tests?\.dll$', re.IGNORECASE),
]


def is_mechanical(func_name, type_name):
    for pat in MECHANICAL_NAME_PATTERNS:
        if pat.search(func_name):
            return True
    for pat in COMPILER_GENERATED_TYPE_PATTERNS:
        if pat.search(type_name):
            return True
    return False


def is_test_module(module_name):
    for pat in TEST_MODULE_PATTERNS:
        if pat.search(module_name):
            return True
    return False


def simplify_name(full_name):
    paren_idx = full_name.find('(')
    if paren_idx == -1:
        return full_name
    method_part = full_name[:paren_idx]
    params_part = full_name[paren_idx + 1:].rstrip(')')
    if not params_part.strip():
        return full_name
    simplified_params = []
    for param in params_part.split(','):
        param = param.strip()
        short = param.rsplit('.', 1)[-1]
        simplified_params.append(short)
    return f"{method_part}({', '.join(simplified_params)})"


def parse_functions(xml_path, include_mechanical, include_tests, module_filter, namespace_filter):
    """Stream-parse XML and yield (module_name, module_attrs, func_dict) tuples.

    Also yields module-level data as (module_name, module_attrs, None) for summary.
    """
    current_module_name = None
    current_module_attrs = None
    skip_module = False
    current_function = None
    source_files = {}

    try:
        context = ET.iterparse(xml_path, events=("start", "end"))
    except FileNotFoundError:
        print(json.dumps({"error": f"File not found: {xml_path}", "type": "file_not_found"}))
        sys.exit(1)
    except ET.ParseError as e:
        print(json.dumps({"error": f"XML parse error: {e}", "type": "parse_error"}))
        sys.exit(1)

    for event, elem in context:
        if event == "start" and elem.tag == "module":
            module_name = elem.get("name", "")
            skip_module = False

            if not include_tests and is_test_module(module_name):
                skip_module = True
                current_module_name = None
                continue

            if module_filter and module_filter.lower() not in module_name.lower():
                skip_module = True
                current_module_name = None
                continue

            current_module_name = module_name
            current_module_attrs = {
                "line_coverage": float(elem.get("line_coverage", 0)),
                "block_coverage": float(elem.get("block_coverage", 0)),
                "lines_covered": int(elem.get("lines_covered", 0)),
                "lines_not_covered": int(elem.get("lines_not_covered", 0)),
                "lines_partially_covered": int(elem.get("lines_partially_covered", 0)),
            }
            source_files = {}

        elif event == "end" and elem.tag == "source_file":
            if current_module_name is not None:
                source_files[elem.get("id", "")] = elem.get("path", "")

        elif event == "start" and elem.tag == "function":
            if skip_module or current_module_name is None:
                current_function = None
                continue

            func_name = elem.get("name", "")
            type_name = elem.get("type_name", "")
            namespace = elem.get("namespace", "")

            if not include_mechanical and is_mechanical(func_name, type_name):
                current_function = None
                continue

            if namespace_filter and namespace_filter.lower() not in namespace.lower():
                current_function = None
                continue

            current_function = {
                "name": simplify_name(func_name),
                "full_name": func_name,
                "namespace": namespace,
                "type_name": type_name,
                "line_coverage": float(elem.get("line_coverage", 0)),
                "block_coverage": float(elem.get("block_coverage", 0)),
                "lines_covered": int(elem.get("lines_covered", 0)),
                "lines_not_covered": int(elem.get("lines_not_covered", 0)),
                "lines_partially_covered": int(elem.get("lines_partially_covered", 0)),
                "uncovered_line_ranges": [],
                "source_file": None,
                "module": current_module_name,
            }

        elif event == "end" and elem.tag == "range":
            if current_function is None:
                continue
            covered = elem.get("covered", "")
            source_id = elem.get("source_id", "")
            if current_function["source_file"] is None:
                current_function["source_file"] = source_files.get(source_id, f"source_id:{source_id}")
            if covered in ("no", "partial"):
                current_function["uncovered_line_ranges"].append(
                    [int(elem.get("start_line", 0)), int(elem.get("end_line", 0))]
                )

        elif event == "end" and elem.tag == "function":
            if current_module_name is not None and current_function is not None:
                yield (current_module_name, current_module_attrs, current_function)
            current_function = None
            elem.clear()

        elif event == "end" and elem.tag == "module":
            if current_module_name is not None:
                yield (current_module_name, current_module_attrs, None)  # module-level sentinel
            current_module_name = None
            skip_module = False
            source_files = {}
            elem.clear()


def merge_line_ranges(ranges):
    """Merge overlapping/adjacent [start, end] ranges."""
    if not ranges:
        return []
    sorted_ranges = sorted(ranges)
    merged = [sorted_ranges[0]]
    for start, end in sorted_ranges[1:]:
        if start <= merged[-1][1] + 1:
            merged[-1][1] = max(merged[-1][1], end)
        else:
            merged.append([start, end])
    return merged


def summary_mode(xml_path, include_mechanical, include_tests, module_filter, namespace_filter):
    modules = {}  # name -> stats
    func_counts = defaultdict(lambda: {"total": 0, "fully_covered": 0, "partially_covered": 0, "uncovered": 0})

    for mod_name, mod_attrs, func in parse_functions(
        xml_path, include_mechanical, include_tests, module_filter, namespace_filter
    ):
        if mod_name not in modules:
            modules[mod_name] = dict(mod_attrs)

        if func is not None:
            func_counts[mod_name]["total"] += 1
            if func["lines_not_covered"] == 0 and func["lines_partially_covered"] == 0:
                func_counts[mod_name]["fully_covered"] += 1
            elif func["lines_covered"] == 0:
                func_counts[mod_name]["uncovered"] += 1
            else:
                func_counts[mod_name]["partially_covered"] += 1

    # Build output
    total_covered = sum(m["lines_covered"] for m in modules.values())
    total_not_covered = sum(m["lines_not_covered"] for m in modules.values())
    total_partial = sum(m["lines_partially_covered"] for m in modules.values())
    total_lines = total_covered + total_not_covered + total_partial
    overall_pct = round(total_covered / total_lines * 100, 2) if total_lines > 0 else 0.0

    module_list = []
    for name in sorted(modules.keys(), key=lambda n: modules[n]["line_coverage"]):
        m = modules[name]
        fc = func_counts[name]
        module_list.append({
            "name": name,
            "line_coverage_pct": m["line_coverage"],
            "block_coverage_pct": m["block_coverage"],
            "lines_covered": m["lines_covered"],
            "lines_not_covered": m["lines_not_covered"],
            "lines_partially_covered": m["lines_partially_covered"],
            "functions_total": fc["total"],
            "functions_fully_covered": fc["fully_covered"],
            "functions_partially_covered": fc["partially_covered"],
            "functions_uncovered": fc["uncovered"],
        })

    return {
        "mode": "summary",
        "overall": {
            "line_coverage_pct": overall_pct,
            "lines_covered": total_covered,
            "lines_not_covered": total_not_covered,
            "lines_partially_covered": total_partial,
            "modules_analyzed": len(modules),
            "functions_analyzed": sum(fc["total"] for fc in func_counts.values()),
        },
        "modules": module_list,
    }


def gaps_mode(xml_path, include_mechanical, include_tests, module_filter, namespace_filter,
              top_n, group_by):
    functions = []
    for mod_name, mod_attrs, func in parse_functions(
        xml_path, include_mechanical, include_tests, module_filter, namespace_filter
    ):
        if func is not None:
            functions.append(func)

    if group_by == "function":
        items = []
        # Sort by uncovered lines descending
        functions.sort(key=lambda f: f["lines_not_covered"] + f["lines_partially_covered"], reverse=True)
        for rank, f in enumerate(functions[:top_n], 1):
            items.append({
                "rank": rank,
                "module": f["module"],
                "namespace": f["namespace"],
                "type_name": f["type_name"],
                "function_name": f["name"],
                "full_name": f["full_name"],
                "line_coverage_pct": f["line_coverage"],
                "lines_not_covered": f["lines_not_covered"],
                "lines_partially_covered": f["lines_partially_covered"],
                "source_file": f["source_file"],
                "uncovered_line_ranges": merge_line_ranges(f["uncovered_line_ranges"]),
            })
        return {"mode": "gaps", "top": top_n, "group_by": "function", "items": items}

    elif group_by == "type":
        type_groups = defaultdict(lambda: {
            "module": "", "namespace": "", "type_name": "",
            "lines_covered": 0, "lines_not_covered": 0, "lines_partially_covered": 0,
            "functions": [], "source_files": set(),
        })
        for f in functions:
            key = (f["module"], f["namespace"], f["type_name"])
            g = type_groups[key]
            g["module"] = f["module"]
            g["namespace"] = f["namespace"]
            g["type_name"] = f["type_name"]
            g["lines_covered"] += f["lines_covered"]
            g["lines_not_covered"] += f["lines_not_covered"]
            g["lines_partially_covered"] += f["lines_partially_covered"]
            if f["source_file"]:
                g["source_files"].add(f["source_file"])
            if f["lines_not_covered"] > 0 or f["lines_partially_covered"] > 0:
                g["functions"].append({
                    "name": f["name"],
                    "lines_not_covered": f["lines_not_covered"],
                    "lines_partially_covered": f["lines_partially_covered"],
                    "source_file": f["source_file"],
                    "uncovered_line_ranges": merge_line_ranges(f["uncovered_line_ranges"]),
                })

        sorted_groups = sorted(
            type_groups.values(),
            key=lambda g: g["lines_not_covered"] + g["lines_partially_covered"],
            reverse=True
        )

        items = []
        for rank, g in enumerate(sorted_groups[:top_n], 1):
            total = g["lines_covered"] + g["lines_not_covered"] + g["lines_partially_covered"]
            pct = round(g["lines_covered"] / total * 100, 2) if total > 0 else 0.0
            # Sort uncovered functions within type by uncovered lines desc
            g["functions"].sort(key=lambda f: f["lines_not_covered"] + f["lines_partially_covered"], reverse=True)
            items.append({
                "rank": rank,
                "module": g["module"],
                "namespace": g["namespace"],
                "type_name": g["type_name"],
                "line_coverage_pct": pct,
                "lines_not_covered": g["lines_not_covered"],
                "lines_partially_covered": g["lines_partially_covered"],
                "function_count": len(g["functions"]),
                "uncovered_functions": g["functions"][:10],  # cap per-type detail
                "source_files": sorted(g["source_files"]),
            })
        return {"mode": "gaps", "top": top_n, "group_by": "type", "items": items}

    elif group_by == "namespace":
        ns_groups = defaultdict(lambda: {
            "modules": set(), "namespace": "",
            "lines_covered": 0, "lines_not_covered": 0, "lines_partially_covered": 0,
            "type_names": set(), "source_files": set(),
        })
        for f in functions:
            key = f["namespace"]
            g = ns_groups[key]
            g["namespace"] = f["namespace"]
            g["modules"].add(f["module"])
            g["type_names"].add(f["type_name"])
            g["lines_covered"] += f["lines_covered"]
            g["lines_not_covered"] += f["lines_not_covered"]
            g["lines_partially_covered"] += f["lines_partially_covered"]
            if f["source_file"]:
                g["source_files"].add(f["source_file"])

        sorted_groups = sorted(
            ns_groups.values(),
            key=lambda g: g["lines_not_covered"] + g["lines_partially_covered"],
            reverse=True
        )

        items = []
        for rank, g in enumerate(sorted_groups[:top_n], 1):
            total = g["lines_covered"] + g["lines_not_covered"] + g["lines_partially_covered"]
            pct = round(g["lines_covered"] / total * 100, 2) if total > 0 else 0.0
            items.append({
                "rank": rank,
                "modules": sorted(g["modules"]),
                "namespace": g["namespace"],
                "line_coverage_pct": pct,
                "lines_not_covered": g["lines_not_covered"],
                "lines_partially_covered": g["lines_partially_covered"],
                "type_count": len(g["type_names"]),
                "source_files": sorted(g["source_files"]),
            })
        return {"mode": "gaps", "top": top_n, "group_by": "namespace", "items": items}

    elif group_by == "module":
        # This is similar to summary but sorted by gap size
        modules = {}
        for mod_name, mod_attrs, func in parse_functions(
            xml_path, include_mechanical, include_tests, module_filter, namespace_filter
        ):
            if mod_name not in modules:
                modules[mod_name] = dict(mod_attrs)
                modules[mod_name]["func_count"] = 0
            if func is not None:
                modules[mod_name]["func_count"] += 1

        sorted_mods = sorted(
            modules.items(),
            key=lambda kv: kv[1]["lines_not_covered"] + kv[1]["lines_partially_covered"],
            reverse=True
        )

        items = []
        for rank, (name, m) in enumerate(sorted_mods[:top_n], 1):
            items.append({
                "rank": rank,
                "module": name,
                "line_coverage_pct": m["line_coverage"],
                "lines_not_covered": m["lines_not_covered"],
                "lines_partially_covered": m["lines_partially_covered"],
                "functions_analyzed": m["func_count"],
            })
        return {"mode": "gaps", "top": top_n, "group_by": "module", "items": items}


def main():
    parser = argparse.ArgumentParser(description="Coverage report — summary or gap analysis")
    parser.add_argument("xml_path", help="Path to coverage XML file")
    parser.add_argument("--mode", required=True, choices=["summary", "gaps"],
                        help="Report mode")
    parser.add_argument("--top", type=int, default=20,
                        help="Number of items in gaps mode (default: 20)")
    parser.add_argument("--group-by", default="function",
                        choices=["function", "type", "namespace", "module"],
                        help="How to group gaps (default: function)")
    parser.add_argument("--include-mechanical", action="store_true")
    parser.add_argument("--include-tests", action="store_true")
    parser.add_argument("--module", dest="module_filter", default=None)
    parser.add_argument("--namespace", dest="namespace_filter", default=None)

    args = parser.parse_args()

    if args.mode == "summary":
        result = summary_mode(args.xml_path, args.include_mechanical, args.include_tests,
                              args.module_filter, args.namespace_filter)
    else:
        result = gaps_mode(args.xml_path, args.include_mechanical, args.include_tests,
                           args.module_filter, args.namespace_filter, args.top, args.group_by)

    json.dump(result, sys.stdout, indent=2)
    print()


if __name__ == "__main__":
    main()
