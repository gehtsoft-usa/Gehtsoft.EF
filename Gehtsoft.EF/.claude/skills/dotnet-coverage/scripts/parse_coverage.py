#!/usr/bin/env python3
"""Core parser for dotnet-coverage XML files.

Parses the XML format produced by `dotnet-coverage collect -f xml` or
`dotnet-coverage merge -f xml` into structured JSON.

Uses iterparse for memory efficiency on large files (80K+ lines).
"""

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET


# --- Mechanical method filters ---

MECHANICAL_NAME_PATTERNS = [
    re.compile(r'^get_'),
    re.compile(r'^set_'),
    re.compile(r'^\.c(?:c)?tor'),
    re.compile(r'^Dispose(?:\(|$)'),
    re.compile(r'^ToString(?:\(|$)'),
    re.compile(r'^GetHashCode(?:\(|$)'),
    re.compile(r'^Equals(?:\(|$)'),
    re.compile(r'^op_'),           # operator overloads
    re.compile(r'^CompareTo(?:\(|$)'),
]

COMPILER_GENERATED_TYPE_PATTERNS = [
    re.compile(r'<>c__DisplayClass'),
    re.compile(r'<.*>d__'),
    re.compile(r'<.*>g__'),
    re.compile(r'\$'),             # record synthesized types
]

TEST_MODULE_PATTERNS = [
    re.compile(r'\.Tests?\.dll$', re.IGNORECASE),
]


def is_mechanical(func_name, type_name):
    """Check if a function is mechanical (getter/setter/ctor/etc)."""
    for pat in MECHANICAL_NAME_PATTERNS:
        if pat.search(func_name):
            return True
    for pat in COMPILER_GENERATED_TYPE_PATTERNS:
        if pat.search(type_name):
            return True
    return False


def is_test_module(module_name):
    """Check if a module is a test assembly."""
    for pat in TEST_MODULE_PATTERNS:
        if pat.search(module_name):
            return True
    return False


def simplify_name(full_name):
    """Simplify a fully-qualified method signature for display.

    E.g. 'BuildQuery(System.Text.StringBuilder, Gehtsoft.EF.Db.SqlDb.QueryBuilder.TableDescriptor.ColumnInfo)'
    becomes 'BuildQuery(StringBuilder, ColumnInfo)'
    """
    # Extract method name and parameters
    paren_idx = full_name.find('(')
    if paren_idx == -1:
        return full_name

    method_part = full_name[:paren_idx]
    params_part = full_name[paren_idx + 1:].rstrip(')')

    if not params_part.strip():
        return full_name

    # Simplify each parameter type to its short name
    simplified_params = []
    for param in params_part.split(','):
        param = param.strip()
        # Take last segment after '.'
        short = param.rsplit('.', 1)[-1]
        simplified_params.append(short)

    return f"{method_part}({', '.join(simplified_params)})"


def parse_coverage_xml(xml_path, include_mechanical=False, include_tests=False,
                       module_filter=None, namespace_filter=None,
                       min_uncovered_lines=0):
    """Parse a dotnet-coverage XML file and return structured data.

    Args:
        xml_path: Path to the XML file.
        include_mechanical: If True, include mechanical methods.
        include_tests: If True, include test assembly modules.
        module_filter: If set, only include modules whose name contains this string.
        namespace_filter: If set, only include functions whose namespace contains this string.
        min_uncovered_lines: Only include functions with at least this many uncovered lines.

    Returns:
        dict with 'summary' and 'modules' keys.
    """
    result = {
        "file": xml_path,
        "summary": {
            "total_modules": 0,
            "analyzed_modules": 0,
            "excluded_test_modules": [],
            "excluded_filtered_modules": [],
            "total_functions": 0,
            "analyzed_functions": 0,
            "excluded_mechanical": 0,
            "excluded_namespace_filter": 0,
            "total_lines_covered": 0,
            "total_lines_not_covered": 0,
            "total_lines_partially_covered": 0,
            "overall_line_coverage_pct": 0.0,
        },
        "modules": [],
    }

    # State for iterparse
    current_module = None
    current_function = None
    source_files = {}  # id -> path, per module

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
            result["summary"]["total_modules"] += 1

            # Check test module filter
            if not include_tests and is_test_module(module_name):
                result["summary"]["excluded_test_modules"].append(module_name)
                current_module = None
                continue

            # Check module name filter
            if module_filter and module_filter.lower() not in module_name.lower():
                result["summary"]["excluded_filtered_modules"].append(module_name)
                current_module = None
                continue

            current_module = {
                "name": module_name,
                "line_coverage": float(elem.get("line_coverage", 0)),
                "block_coverage": float(elem.get("block_coverage", 0)),
                "lines_covered": int(elem.get("lines_covered", 0)),
                "lines_not_covered": int(elem.get("lines_not_covered", 0)),
                "lines_partially_covered": int(elem.get("lines_partially_covered", 0)),
                "source_files": {},
                "functions": [],
            }
            source_files = {}

        elif event == "end" and elem.tag == "source_file":
            if current_module is not None:
                fid = elem.get("id", "")
                fpath = elem.get("path", "")
                source_files[fid] = fpath
                current_module["source_files"][fid] = fpath

        elif event == "start" and elem.tag == "function":
            if current_module is None:
                continue

            func_name = elem.get("name", "")
            type_name = elem.get("type_name", "")
            namespace = elem.get("namespace", "")
            result["summary"]["total_functions"] += 1

            # Mechanical filter
            if not include_mechanical and is_mechanical(func_name, type_name):
                result["summary"]["excluded_mechanical"] += 1
                current_function = None
                continue

            # Namespace filter
            if namespace_filter and namespace_filter.lower() not in namespace.lower():
                result["summary"]["excluded_namespace_filter"] += 1
                current_function = None
                continue

            lines_not_covered = int(elem.get("lines_not_covered", 0))
            lines_partially = int(elem.get("lines_partially_covered", 0))

            # Min uncovered lines filter
            if min_uncovered_lines > 0 and (lines_not_covered + lines_partially) < min_uncovered_lines:
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
                "lines_not_covered": lines_not_covered,
                "lines_partially_covered": lines_partially,
                "uncovered_ranges": [],
                "source_file": None,
            }

        elif event == "end" and elem.tag == "range":
            if current_function is None:
                continue
            covered = elem.get("covered", "")
            if covered in ("no", "partial"):
                source_id = elem.get("source_id", "")
                source_path = source_files.get(source_id, f"source_id:{source_id}")
                current_function["uncovered_ranges"].append({
                    "source_file": source_path,
                    "start_line": int(elem.get("start_line", 0)),
                    "end_line": int(elem.get("end_line", 0)),
                    "covered": covered,
                })
                # Set source_file from first range
                if current_function["source_file"] is None:
                    current_function["source_file"] = source_path

            # Also capture source_file from covered ranges if not yet set
            if current_function["source_file"] is None and covered == "yes":
                source_id = elem.get("source_id", "")
                current_function["source_file"] = source_files.get(source_id, f"source_id:{source_id}")

        elif event == "end" and elem.tag == "function":
            if current_module is not None and current_function is not None:
                current_module["functions"].append(current_function)
                result["summary"]["analyzed_functions"] += 1
            current_function = None
            elem.clear()

        elif event == "end" and elem.tag == "module":
            if current_module is not None:
                result["modules"].append(current_module)
                result["summary"]["analyzed_modules"] += 1
                result["summary"]["total_lines_covered"] += current_module["lines_covered"]
                result["summary"]["total_lines_not_covered"] += current_module["lines_not_covered"]
                result["summary"]["total_lines_partially_covered"] += current_module["lines_partially_covered"]
            current_module = None
            source_files = {}
            elem.clear()

    # Compute overall coverage
    total_lines = (result["summary"]["total_lines_covered"] +
                   result["summary"]["total_lines_not_covered"] +
                   result["summary"]["total_lines_partially_covered"])
    if total_lines > 0:
        result["summary"]["overall_line_coverage_pct"] = round(
            result["summary"]["total_lines_covered"] / total_lines * 100, 2
        )

    return result


def main():
    parser = argparse.ArgumentParser(
        description="Parse dotnet-coverage XML to JSON"
    )
    parser.add_argument("xml_path", help="Path to coverage XML file")
    parser.add_argument("--include-mechanical", action="store_true",
                        help="Include mechanical methods (getters/setters/ctors/etc)")
    parser.add_argument("--include-tests", action="store_true",
                        help="Include test assembly modules (*.Test.dll)")
    parser.add_argument("--module", dest="module_filter", default=None,
                        help="Filter to modules containing this string")
    parser.add_argument("--namespace", dest="namespace_filter", default=None,
                        help="Filter to functions in namespaces containing this string")
    parser.add_argument("--min-uncovered-lines", type=int, default=0,
                        help="Only include functions with at least N uncovered lines")

    args = parser.parse_args()

    data = parse_coverage_xml(
        args.xml_path,
        include_mechanical=args.include_mechanical,
        include_tests=args.include_tests,
        module_filter=args.module_filter,
        namespace_filter=args.namespace_filter,
        min_uncovered_lines=args.min_uncovered_lines,
    )

    json.dump(data, sys.stdout, indent=2)
    print()


if __name__ == "__main__":
    main()
