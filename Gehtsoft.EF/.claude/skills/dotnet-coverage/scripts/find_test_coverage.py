#!/usr/bin/env python3
"""Per-test coverage analysis.

Compares a full-suite coverage XML with a single-test coverage XML to show
what code the test exercises and where it contributes to weakly-covered areas.
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


def parse_function_coverage(xml_path, include_mechanical=False, include_tests=False):
    """Parse XML and return a dict of function coverage data.

    Returns:
        dict keyed by (module_name, namespace, type_name, func_name) ->
            {lines_covered, lines_not_covered, lines_partially_covered,
             covered_lines: set, uncovered_lines: set, source_file}
    """
    functions = {}
    current_module_name = None
    skip_module = False
    current_function_key = None
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

            current_module_name = module_name
            source_files = {}

        elif event == "end" and elem.tag == "source_file":
            if current_module_name is not None:
                source_files[elem.get("id", "")] = elem.get("path", "")

        elif event == "start" and elem.tag == "function":
            if skip_module or current_module_name is None:
                current_function_key = None
                current_function = None
                continue

            func_name = elem.get("name", "")
            type_name = elem.get("type_name", "")
            namespace = elem.get("namespace", "")

            if not include_mechanical and is_mechanical(func_name, type_name):
                current_function_key = None
                current_function = None
                continue

            current_function_key = (current_module_name, namespace, type_name, func_name)
            current_function = {
                "module": current_module_name,
                "namespace": namespace,
                "type_name": type_name,
                "name": simplify_name(func_name),
                "full_name": func_name,
                "line_coverage": float(elem.get("line_coverage", 0)),
                "lines_covered": int(elem.get("lines_covered", 0)),
                "lines_not_covered": int(elem.get("lines_not_covered", 0)),
                "lines_partially_covered": int(elem.get("lines_partially_covered", 0)),
                "covered_lines": set(),
                "uncovered_lines": set(),
                "source_file": None,
            }

        elif event == "end" and elem.tag == "range":
            if current_function is None:
                continue
            covered = elem.get("covered", "")
            source_id = elem.get("source_id", "")
            start_line = int(elem.get("start_line", 0))
            end_line = int(elem.get("end_line", 0))

            if current_function["source_file"] is None:
                current_function["source_file"] = source_files.get(source_id, f"source_id:{source_id}")

            line_set = set(range(start_line, end_line + 1))
            if covered == "yes":
                current_function["covered_lines"].update(line_set)
            elif covered == "no":
                current_function["uncovered_lines"].update(line_set)
            else:  # partial
                current_function["covered_lines"].update(line_set)
                current_function["uncovered_lines"].update(line_set)

        elif event == "end" and elem.tag == "function":
            if current_function_key is not None and current_function is not None:
                functions[current_function_key] = current_function
            current_function_key = None
            current_function = None
            elem.clear()

        elif event == "end" and elem.tag == "module":
            current_module_name = None
            skip_module = False
            source_files = {}
            elem.clear()

    return functions


def compare_coverage(full_funcs, test_funcs):
    """Compare full-suite and single-test coverage.

    Returns structured analysis of what the test covers and where it
    contributes to weakly-covered areas.
    """
    # What the test covers
    test_modules = defaultdict(lambda: {"functions_covered": 0, "lines_covered": 0, "functions": []})

    for key, tf in test_funcs.items():
        if tf["lines_covered"] == 0 and not tf["covered_lines"]:
            continue

        mod = test_modules[tf["module"]]
        mod["functions_covered"] += 1
        mod["lines_covered"] += tf["lines_covered"]
        mod["functions"].append({
            "namespace": tf["namespace"],
            "type_name": tf["type_name"],
            "function_name": tf["name"],
            "lines_covered_by_test": tf["lines_covered"],
        })

    test_coverage = {
        "modules_touched": len(test_modules),
        "total_functions_covered": sum(m["functions_covered"] for m in test_modules.values()),
        "total_lines_covered": sum(m["lines_covered"] for m in test_modules.values()),
        "modules": [],
    }

    for mod_name in sorted(test_modules.keys()):
        m = test_modules[mod_name]
        # Sort functions by lines covered desc
        m["functions"].sort(key=lambda f: f["lines_covered_by_test"], reverse=True)
        test_coverage["modules"].append({
            "name": mod_name,
            "functions_covered": m["functions_covered"],
            "lines_covered": m["lines_covered"],
            "functions": m["functions"][:20],  # cap detail
        })

    # Coverage contribution: areas where the full suite has weak coverage
    # and this test covers some of those lines
    contributions = []

    for key, tf in test_funcs.items():
        if not tf["covered_lines"]:
            continue

        ff = full_funcs.get(key)
        if ff is None:
            continue

        # The test covers lines that are weakly covered in the full suite
        full_coverage_pct = ff["line_coverage"]
        if full_coverage_pct >= 90:
            continue  # well-covered in full suite, skip

        # Lines the test covers that are uncovered in full suite
        # Since the full suite includes this test, these are lines where
        # this test is possibly the sole contributor
        test_covers = tf["covered_lines"]
        full_uncovered = ff["uncovered_lines"]
        overlap = test_covers & full_uncovered  # partial lines may appear in both

        contributions.append({
            "module": tf["module"],
            "namespace": tf["namespace"],
            "type_name": tf["type_name"],
            "function_name": tf["name"],
            "full_suite_line_coverage_pct": full_coverage_pct,
            "test_covers_lines_count": len(test_covers),
            "full_suite_uncovered_lines_count": len(full_uncovered),
            "overlapping_lines": len(overlap),
            "source_file": tf["source_file"],
        })

    # Sort by how weak the full coverage is (lowest first) and overlap size
    contributions.sort(key=lambda c: (c["full_suite_line_coverage_pct"], -c["overlapping_lines"]))

    coverage_contribution = {
        "description": (
            "Functions where the full suite has weak coverage (<90%) and this test "
            "exercises some code. Since the full suite includes this test, these are "
            "areas where this test is an important contributor to coverage."
        ),
        "functions_in_weak_areas": len(contributions),
        "items": contributions[:30],  # cap
    }

    return {
        "test_coverage": test_coverage,
        "coverage_contribution": coverage_contribution,
    }


def main():
    parser = argparse.ArgumentParser(description="Per-test coverage analysis")
    parser.add_argument("--full", required=True, help="Path to full-suite coverage XML")
    parser.add_argument("--test", required=True, help="Path to single-test coverage XML")
    parser.add_argument("--include-mechanical", action="store_true")
    parser.add_argument("--include-tests", action="store_true")

    args = parser.parse_args()

    full_funcs = parse_function_coverage(args.full, args.include_mechanical, args.include_tests)
    test_funcs = parse_function_coverage(args.test, args.include_mechanical, args.include_tests)

    result = compare_coverage(full_funcs, test_funcs)
    json.dump(result, sys.stdout, indent=2)
    print()


if __name__ == "__main__":
    main()
