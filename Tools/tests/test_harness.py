"""Offline regression checks for false green results and Unity meta hygiene."""

from importlib.machinery import SourceFileLoader
from importlib.util import module_from_spec, spec_from_loader
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from io import StringIO

loader = SourceFileLoader("harness", str(Path(__file__).resolve().parents[1] / "harness"))
spec = spec_from_loader(loader.name, loader)
harness = module_from_spec(spec)
loader.exec_module(harness)


def envelope(result):
    return {"success": True, "data": {"success": True, "result": result}}


def completed(**overrides):
    summary = dict(total=3, passed=3, failed=0, skipped=0, inconclusive=0)
    summary.update(overrides)
    return dict(status="completed", summary=summary)


class ResultsTests(unittest.TestCase):
    def test_nested_success_and_json_string_test_results(self):
        self.assertEqual([], harness.validate_result(envelope(json.dumps(completed())), tests=True))
        self.assertEqual([], harness.validate_result(envelope({"success": True, "result": 4})))

    def test_outer_or_nested_failure_never_passes(self):
        for value in ({"success": False}, {"success": "true"}, envelope({"success": False}),
                      envelope(json.dumps({"success": False})), {"data": {"success": True}},
                      envelope({"success": True, "errors": ["failure"]})):
            with self.subTest(value=value):
                self.assertTrue(harness.validate_result(value))

    def test_running_missing_zero_partial_skipped_inconclusive_do_not_pass(self):
        values = [dict(status="running", summary=completed()["summary"]), {},
                  completed(total=0, passed=0), completed(passed=2), completed(failed=1),
                  completed(skipped=1), completed(inconclusive=1), completed(total=True),
                  completed(total="3"), completed(total=-1)]
        del_count = completed()
        del del_count["summary"]["skipped"]
        values.append(del_count)
        for value in values:
            with self.subTest(value=value):
                self.assertTrue(harness.validate_result(envelope(value), tests=True))

    def test_pipeline_capitalized_keys(self):
        result = {"Status": "Completed", "Summary": {k.title(): v for k, v in completed()["summary"].items()}}
        self.assertEqual([], harness.validate_result(envelope(result), tests=True))

    def test_summary_cannot_hide_failed_or_missing_test_entries(self):
        for results in ([{"Status": "Passed"}], [{"Status": "Passed"}] * 2 + [{"Status": "Failed"}]):
            result = dict(completed(), results=results)
            self.assertTrue(harness.validate_result(envelope(result), tests=True))

    def test_json_null_is_not_a_successful_envelope(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "result.json"
            path.write_text("null")
            with patch("sys.argv", ["harness", "result", str(path), "--json"]), patch("sys.stdout", new_callable=StringIO) as output:
                self.assertEqual(1, harness.main())
            self.assertFalse(json.loads(output.getvalue())["success"])

    def test_editor_requires_project_and_explicit_ready_flags(self):
        state = dict(projectPath=str(harness.ROOT), status="ready", compiling=False, domainReloadInProgress=False)
        self.assertEqual([], harness.editor_ready(envelope(state)))
        for key, value in (("projectPath", "/another/project"), ("status", "playing"),
                           ("compiling", True), ("domainReloadInProgress", True)):
            self.assertTrue(harness.editor_ready(envelope(dict(state, **{key: value}))))
        del state["compiling"]
        self.assertTrue(harness.editor_ready(envelope(state)))

    def test_host_refusal_is_preserved_without_second_command(self):
        with patch.object(harness, "cli_read", return_value=(77, None, ["host access required"])) as read:
            code, failures = harness.preflight()
        self.assertEqual(77, code)
        self.assertTrue(failures)
        self.assertEqual(1, read.call_count)


class AssetTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.base = self.root / "Assets/SinkLab"
        self.base.mkdir(parents=True)
        self.meta(self.base, "a" * 32)

    def meta(self, asset, guid):
        Path(str(asset) + ".meta").write_text("fileFormatVersion: 2\nguid: " + guid + "\n")

    def test_complete_files_and_directory_metadata(self):
        folder = self.base / "Parts"
        folder.mkdir()
        asset = folder / "Food.prefab"
        asset.write_text("test fixture")
        self.meta(folder, "b" * 32)
        self.meta(asset, "c" * 32)
        self.assertEqual([], harness.asset_checks(self.root))

    def test_missing_orphan_duplicate_and_bad_guid_are_distinct(self):
        missing = self.base / "MissingDirectory"
        missing.mkdir()
        self.meta(self.base / "Orphan.prefab", "b" * 32)
        asset = self.base / "Duplicate.prefab"
        asset.write_text("test fixture")
        self.meta(asset, "a" * 32)
        bad = self.base / "Bad.prefab"
        bad.write_text("test fixture")
        self.meta(bad, "not-a-guid")
        failures = harness.asset_checks(self.root)
        for message in ("Missing meta", "Orphan meta", "Duplicate GUID", "valid GUID"):
            self.assertTrue(any(message in failure for failure in failures), failures)

    def test_wrong_physics_extension_and_invalid_assembly(self):
        for name, content in (("Wet.physicsMaterial", "fixture"), ("Bad.asmdef", "{")):
            (self.base / name).write_text(content)
        failures = harness.asset_checks(self.root)
        self.assertTrue(any("Wrong physics material extension" in f for f in failures))
        self.assertTrue(any("cannot read valid JSON" in f for f in failures))


if __name__ == "__main__":
    unittest.main()
