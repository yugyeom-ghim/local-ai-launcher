import hashlib
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from build_full_release import split_archive


class DistributionTests(unittest.TestCase):
    def test_round_trip_at_part_boundaries(self):
        for size in (9999, 10000, 10001, 25600):
            with self.subTest(size=size), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                source = root / "full.zip"
                data = (bytes(range(256)) * 101)[:size]
                source.write_bytes(data)
                result = split_archive(source, root, "https://example.invalid/", part_size=10000)
                parts = result["parts"]
                self.assertEqual(b"".join((root / p["name"]).read_bytes() for p in parts), data)
                self.assertEqual(result["archive"]["sha256"], hashlib.sha256(data).hexdigest())
                self.assertEqual(sum(p["size"] for p in parts), size)
                for part in parts:
                    content = (root / part["name"]).read_bytes()
                    self.assertEqual(part["sha256"], hashlib.sha256(content).hexdigest())
                    self.assertLessEqual(len(content), 10000)

    def test_existing_part_not_overwritten(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source = root / "full.zip"
            source.write_bytes(b"new")
            part = root / "full.zip.part01"
            part.write_bytes(b"existing")
            with self.assertRaises(FileExistsError):
                split_archive(source, root, "https://example.invalid/")
            self.assertEqual(part.read_bytes(), b"existing")


if __name__ == "__main__":
    unittest.main()
