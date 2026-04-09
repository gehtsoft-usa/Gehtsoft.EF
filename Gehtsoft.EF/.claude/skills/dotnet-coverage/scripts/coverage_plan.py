#!/usr/bin/env python3
"""Coverage improvement plan generator.

Groups uncovered code by functional area (namespace), identifies quick wins,
and computes gap-to-target for each module.
"""

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict


# --- Mechanical method filters (same as other scripts) ---

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


def parse_all(xml_path, include_mechanical, include_tests, module_filter, namespace_filter):
    """Parse XML and return (modules_dict, functions_list).

    modules_dict: name -> {line_coverage, lines_covered, lines_not_covered, ...}
    functions_list: list of func dicts with module, namespace, type_name, etc.
    """
    modules = {}
    functions = []
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
                "module": current_module_name,
                "line_coverage": float(elem.get("line_coverage", 0)),
                "lines_covered": int(elem.get("lines_covered", 0)),
                "lines_not_covered": int(elem.get("lines_not_covered", 0)),
                "lines_partially_covered": int(elem.get("lines_partially_covered", 0)),
                "source_file": None,
                "start_line": None,
                "end_line": None,
            }

        elif event == "end" and elem.tag == "range":
            if current_function is None:
                continue
            source_id = elem.get("source_id", "")
            start_line = int(elem.get("start_line", 0))
            end_line = int(elem.get("end_line", 0))

            if current_function["source_file"] is None:
                current_function["source_file"] = source_files.get(source_id, f"source_id:{source_id}")
            if current_function["start_line"] is None or start_line < current_function["start_line"]:
                current_function["start_line"] = start_line
            if current_function["end_line"] is None or end_line > current_function["end_line"]:
                current_function["end_line"] = end_line

        elif event == "end" and elem.tag == "function":
            if current_module_name is not None and current_function is not None:
                functions.append(current_function)
            current_function = None
            elem.clear()

        elif event == "end" and elem.tag == "module":
            if current_module_name is not None:
                modules[current_module_name] = current_module_attrs
            current_module_name = None
            skip_module = False
            source_files = {}
            elem.clear()

    return modules, functions


def build_plan(modules, functions, target_coverage, max_areas):
    # Overall stats
    total_covered = sum(m["lines_covered"] for m in modules.values())
    total_not_covered = sum(m["lines_not_covered"] for m in modules.values())
    total_partial = sum(m["lines_partially_covered"] for m in modules.values())
    total_lines = total_covered + total_not_covered + total_partial
    current_pct = round(total_covered / total_lines * 100, 2) if total_lines > 0 else 0.0

    # Lines needed to reach target
    target_covered = int(total_lines * target_coverage / 100)
    lines_to_target = max(0, target_covered - total_covered)

    # Group functions by namespace
    ns_groups = defaultdict(lambda: {
        "namespace": "",
        "modules": set(),
        "types": defaultdict(lambda: {
            "type_name": "",
            "lines_covered": 0,
            "lines_not_covered": 0,
            "lines_partially_covered": 0,
            "uncovered_functions": [],
            "source_files": set(),
        }),
        "source_files": set(),
        "total_lines_covered": 0,
        "total_lines_not_covered": 0,
        "total_lines_partially_covered": 0,
    })

    for f in functions:
        ns = f["namespace"]
        g = ns_groups[ns]
        g["namespace"] = ns
        g["modules"].add(f["module"])
        g["total_lines_covered"] += f["lines_covered"]
        g["total_lines_not_covered"] += f["lines_not_covered"]
        g["total_lines_partially_covered"] += f["lines_partially_covered"]
        if f["source_file"]:
            g["source_files"].add(f["source_file"])

        t = g["types"][f["type_name"]]
        t["type_name"] = f["type_name"]
        t["lines_covered"] += f["lines_covered"]
        t["lines_not_covered"] += f["lines_not_covered"]
        t["lines_partially_covered"] += f["lines_partially_covered"]
        if f["source_file"]:
            t["source_files"].add(f["source_file"])

        if f["lines_not_covered"] > 0 or f["lines_partially_covered"] > 0:
            t["uncovered_functions"].append({
                "name": f["name"],
                "lines_not_covered": f["lines_not_covered"],
                "lines_partially_covered": f["lines_partially_covered"],
                "source_file": f["source_file"],
                "start_line": f["start_line"],
                "end_line": f["end_line"],
            })

    # Sort areas by uncovered lines
    sorted_areas = sorted(
        ns_groups.values(),
        key=lambda g: g["total_lines_not_covered"] + g["total_lines_partially_covered"],
        reverse=True
    )

    # Build functional areas output
    functional_areas = []
    for g in sorted_areas[:max_areas]:
        total = g["total_lines_covered"] + g["total_lines_not_covered"] + g["total_lines_partially_covered"]
        pct = round(g["total_lines_covered"] / total * 100, 2) if total > 0 else 0.0

        types_list = []
        for t in sorted(g["types"].values(),
                        key=lambda t: t["lines_not_covered"] + t["lines_partially_covered"],
                        reverse=True):
            if t["lines_not_covered"] == 0 and t["lines_partially_covered"] == 0:
                continue
            t_total = t["lines_covered"] + t["lines_not_covered"] + t["lines_partially_covered"]
            t_pct = round(t["lines_covered"] / t_total * 100, 2) if t_total > 0 else 0.0
            # Sort uncovered functions within type
            t["uncovered_functions"].sort(
                key=lambda f: f["lines_not_covered"] + f["lines_partially_covered"],
                reverse=True
            )
            types_list.append({
                "type_name": t["type_name"],
                "line_coverage_pct": t_pct,
                "lines_not_covered": t["lines_not_covered"],
                "lines_partially_covered": t["lines_partially_covered"],
                "uncovered_functions": t["uncovered_functions"][:10],  # cap detail
                "source_files": sorted(t["source_files"]),
            })

        functional_areas.append({
            "area_name": g["namespace"],
            "modules": sorted(g["modules"]),
            "current_line_coverage_pct": pct,
            "total_lines_not_covered": g["total_lines_not_covered"],
            "total_lines_partially_covered": g["total_lines_partially_covered"],
            "types": types_list[:15],  # cap types per area
            "source_files": sorted(g["source_files"]),
        })

    # Quick wins: functions with 1-5 uncovered lines
    quick_win_funcs = [
        f for f in functions
        if 0 < (f["lines_not_covered"] + f["lines_partially_covered"]) <= 5
    ]
    quick_win_funcs.sort(key=lambda f: f["lines_not_covered"] + f["lines_partially_covered"])

    quick_wins = {
        "description": "Functions with 1-5 uncovered lines — likely a single branch or error path",
        "count": len(quick_win_funcs),
        "total_lines": sum(f["lines_not_covered"] + f["lines_partially_covered"] for f in quick_win_funcs),
        "examples": [
            {
                "module": f["module"],
                "namespace": f["namespace"],
                "type_name": f["type_name"],
                "function_name": f["name"],
                "lines_not_covered": f["lines_not_covered"],
                "lines_partially_covered": f["lines_partially_covered"],
                "source_file": f["source_file"],
            }
            for f in quick_win_funcs[:20]
        ],
    }

    # Modules below target
    modules_below = []
    for name in sorted(modules.keys(), key=lambda n: modules[n]["line_coverage"]):
        m = modules[name]
        if m["line_coverage"] < target_coverage:
            gap = round(target_coverage - m["line_coverage"], 2)
            m_total = m["lines_covered"] + m["lines_not_covered"] + m["lines_partially_covered"]
            lines_needed = max(0, int(m_total * target_coverage / 100) - m["lines_covered"])
            modules_below.append({
                "name": name,
                "line_coverage_pct": m["line_coverage"],
                "gap_to_target": gap,
                "lines_to_cover": lines_needed,
            })

    return {
        "current_coverage": {
            "line_coverage_pct": current_pct,
            "lines_covered": total_covered,
            "lines_not_covered": total_not_covered,
            "lines_partially_covered": total_partial,
            "target_pct": target_coverage,
            "lines_to_reach_target": lines_to_target,
        },
        "functional_areas": functional_areas,
        "quick_wins": quick_wins,
        "modules_below_target": modules_below,
    }


def main():
    parser = argparse.ArgumentParser(description="Coverage improvement plan generator")
    parser.add_argument("xml_path", help="Path to coverage XML file")
    parser.add_argument("--target-coverage", type=float, default=90,
                        help="Target line coverage percentage (default: 90)")
    parser.add_argument("--max-areas", type=int, default=10,
                        help="Maximum number of functional areas to report (default: 10)")
    parser.add_argument("--include-mechanical", action="store_true")
    parser.add_argument("--include-tests", action="store_true")
    parser.add_argument("--module", dest="module_filter", default=None)
    parser.add_argument("--namespace", dest="namespace_filter", default=None)

    args = parser.parse_args()

    modules, functions = parse_all(
        args.xml_path,
        include_mechanical=args.include_mechanical,
        include_tests=args.include_tests,
        module_filter=args.module_filter,
        namespace_filter=args.namespace_filter,
    )

    plan = build_plan(modules, functions, args.target_coverage, args.max_areas)
    json.dump(plan, sys.stdout, indent=2)
    print()


if __name__ == "__main__":
    main()
