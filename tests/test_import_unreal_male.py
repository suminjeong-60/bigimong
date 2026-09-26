"""Protect the editor import from missing parts and silent wrong asset types."""

import hashlib
import importlib.util
import tempfile
import unittest
from pathlib import Path

from test_stage_unreal_male import source_glb


SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
spec = importlib.util.spec_from_file_location("import_unreal_male", SCRIPTS / "import-unreal-male.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
stage_spec = importlib.util.spec_from_file_location("stage_unreal_male", SCRIPTS / "stage-unreal-male.py")
stage_module = importlib.util.module_from_spec(stage_spec)
stage_spec.loader.exec_module(stage_module)


class FakeAsset:
    pass


class FakeStaticMesh(FakeAsset):
    pass


class FakeTask:
    def __init__(self):
        self.properties = {}

    def set_editor_property(self, name, value):
        self.properties[name] = value


class FakeLibrary:
    def __init__(self):
        self.assets = {}

    def does_asset_exist(self, path):
        return path in self.assets

    def load_asset(self, path):
        return self.assets.get(path)


class FakeTools:
    def __init__(self, library):
        self.library = library
        self.calls = []

    def import_asset_tasks(self, tasks):
        self.calls.extend(tasks)
        for task in tasks:
            name = Path(task.properties["filename"]).stem
            self.library.assets[f'{task.properties["destination_path"]}/{name}'] = FakeStaticMesh()


class FakeHelpers:
    def __init__(self, tools):
        self.tools = tools

    def get_asset_tools(self):
        return self.tools


class FakeEngine:
    AssetImportTask = FakeTask
    StaticMesh = FakeStaticMesh

    def __init__(self):
        self.EditorAssetLibrary = FakeLibrary()
        self.tools = FakeTools(self.EditorAssetLibrary)
        self.AssetToolsHelpers = FakeHelpers(self.tools)


class MaleEditorImportTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.raw = Path(self.temp.name) / "parts"
        self.raw.mkdir()
        self.prepared = Path(self.temp.name) / "UnrealImport"
        hashes = {}
        for name in stage_module.PARTS:
            data = source_glb(base=name == "male-base.glb")
            (self.raw / name).write_bytes(data)
            hashes[name] = hashlib.sha256(data).hexdigest()
        stage_module.stage(self.raw, self.prepared, expected_hashes=hashes)

    def test_preflight_recognizes_31_static_parts_and_morph_face(self):
        plan = module.preflight(self.prepared)
        self.assertEqual(len(plan), 31)
        self.assertIn((self.prepared / "SM_MaleBody.glb", "/Game/Varco/Male/SM_MaleBody"), plan)
        self.assertIn((self.prepared / "SM_MaleEye_14.glb", "/Game/Varco/Male/SM_MaleEye_14"), plan)
        self.assertNotIn("SK_MaleFace", [name for _, name in plan])

    def test_preflight_fails_before_import_if_a_variant_is_missing_or_face_loses_morphs(self):
        last_hair = self.prepared / "SM_MaleHair_14.glb"
        original_hair = last_hair.read_bytes()
        last_hair.unlink()
        with self.assertRaisesRegex(ValueError, "SM_MaleHair_14"):
            module.preflight(self.prepared)
        last_hair.write_bytes(original_hair)
        face = self.prepared / "SK_MaleFace.glb"
        face_document, face_binary = stage_module.read_glb(face.read_bytes())
        face_document["meshes"][0]["primitives"][0]["targets"] = []
        face.write_bytes(stage_module.write_glb(face_document, face_binary))
        with self.assertRaisesRegex(ValueError, "face_00|morph"):
            module.preflight(self.prepared)

    def test_imports_only_missing_static_meshes_without_overwriting(self):
        engine = FakeEngine()
        body = "/Game/Varco/Male/SM_MaleBody"
        engine.EditorAssetLibrary.assets[body] = FakeStaticMesh()
        count = module.import_static(module.preflight(self.prepared), engine)
        self.assertEqual(count, 30)
        self.assertEqual(len(engine.tools.calls), 30)
        self.assertTrue(all(isinstance(asset, FakeStaticMesh) for asset in
                            engine.EditorAssetLibrary.assets.values()))
        self.assertNotIn("/Game/Varco/Male/SK_MaleFace", engine.EditorAssetLibrary.assets)
        self.assertTrue(all(task.properties["replace_existing"] is False and
                            task.properties["automated"] is True and
                            task.properties["save"] is True and
                            task.properties["async_"] is False
                            for task in engine.tools.calls))

    def test_existing_wrong_type_or_wrong_import_result_stops_processing(self):
        engine = FakeEngine()
        engine.EditorAssetLibrary.assets["/Game/Varco/Male/SM_MaleBody"] = FakeAsset()
        with self.assertRaisesRegex(RuntimeError, "SM_MaleBody"):
            module.import_static(module.preflight(self.prepared), engine)
        self.assertFalse(engine.tools.calls)

        engine = FakeEngine()
        engine.tools.import_asset_tasks = lambda tasks: None
        with self.assertRaisesRegex(RuntimeError, "SM_MaleBody"):
            module.import_static(module.preflight(self.prepared), engine)


if __name__ == "__main__":
    unittest.main()
