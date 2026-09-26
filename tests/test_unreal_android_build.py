"""A missing engine cannot produce a misleading APK; an available build is checked."""

import os
import subprocess
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "scripts/build-unreal-android.sh"


class UnrealAndroidBuildTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.project = self.root / "Bigimong"
        self.assets = self.project / "Content/Varco/Male"
        self.assets.mkdir(parents=True)
        (self.project / "Bigimong.uproject").write_text("{}")
        self.out = self.root / "output"
        self.engine = self.root / "UnrealEngine"
        self.uat = self.engine / "Engine/Build/BatchFiles/RunUAT.sh"
        self.uat.parent.mkdir(parents=True)
        self.env = os.environ.copy()
        self.env.update({"BIGIMONG_UNREAL_ROOT": str(self.engine),
                         "BIGIMONG_UNREAL_PROJECT_DIR": str(self.project),
                         "BIGIMONG_APK_OUTPUT_DIR": str(self.out)})

    def run_build(self):
        return subprocess.run(["bash", str(SCRIPT)], env=self.env,
                              text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)

    def install_fake_assets(self):
        for name in ("SM_MaleBody", "SK_MaleFace"):
            (self.assets / f"{name}.uasset").write_bytes(b"asset")
        for kind in ("Eye", "Hair"):
            for number in range(15):
                (self.assets / f"SM_Male{kind}_{number:02d}.uasset").write_bytes(b"asset")

    def test_without_unreal_engine_exits_without_apk(self):
        result = self.run_build()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Unreal", result.stdout)
        self.assertFalse(self.out.exists())

    def test_missing_variant_prevents_build(self):
        self.install_fake_assets()
        (self.assets / "SM_MaleHair_14.uasset").unlink()
        self.uat.write_text("#!/bin/sh\necho should-not-run >&2\nexit 90\n")
        self.uat.chmod(0o755)
        result = self.run_build()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("SM_MaleHair_14", result.stdout)
        self.assertNotIn("should-not-run", result.stdout)

    def test_build_artifact_requires_android_manifest_and_arm64_library(self):
        self.install_fake_assets()
        self.uat.write_text("#!/bin/sh\n"
                            "python3 - <<'PY'\n"
                            "import os,zipfile\n"
                            "from pathlib import Path\n"
                            "p=Path(os.environ['BIGIMONG_APK_ARCHIVE_DIR'])/'packaged.apk'\n"
                            "with zipfile.ZipFile(p,'w') as z:\n"
                            " z.writestr('AndroidManifest.xml',b'manifest')\n"
                            " z.writestr('lib/arm64-v8a/libUnreal.so',b'library')\n"
                            "PY\n")
        self.uat.chmod(0o755)
        result = self.run_build()
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertTrue((self.out / "Bigimong-Unreal-MalePreview-debug.apk").exists())
        self.assertTrue((self.out / "Bigimong-Unreal-MalePreview-debug.apk.sha256").exists())

    def test_missing_arm64_library_rejects_package(self):
        self.install_fake_assets()
        self.uat.write_text("#!/bin/sh\n"
                            "python3 - <<'PY'\n"
                            "import os,zipfile\n"
                            "from pathlib import Path\n"
                            "p=Path(os.environ['BIGIMONG_APK_ARCHIVE_DIR'])/'packaged.apk'\n"
                            "with zipfile.ZipFile(p,'w') as z:\n"
                            " z.writestr('AndroidManifest.xml',b'manifest')\n"
                            "PY\n")
        self.uat.chmod(0o755)
        result = self.run_build()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("arm64", result.stdout)
        self.assertFalse((self.out / "Bigimong-Unreal-MalePreview-debug.apk").exists())


if __name__ == "__main__":
    unittest.main()
