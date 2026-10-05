import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("backfill", Path(__file__).with_name("backfill-address-regions.py"))
backfill = importlib.util.module_from_spec(spec)
spec.loader.exec_module(backfill)


class LegacyAddressMatchingTests(unittest.TestCase):
    regions = {
        "states": [{"id": 34, "name": "İSTANBUL"}, {"id": 6, "name": "ANKARA"}],
        "districts": {"34": [{"id": 1, "name": "KADIKÖY"}, {"id": 2, "name": "FATİH"}],
                      "6": [{"id": 104, "name": "ÇANKAYA"}]}
    }

    def test_verified_prefix_extracts_ids_and_keeps_street(self):
        result = backfill.match("istanbul/kadikoy-Test Sokak 1 - B Blok", self.regions)
        self.assertEqual((34, 1, "Test Sokak 1 - B Blok"), (result[0]["id"], result[1]["id"], result[2]))

    def test_verified_terminal_pair_accepts_import_and_geocoder_formats(self):
        for address in ["Test Sokak 1 Kadıköy İstanbul", "Test Sokak 1, Kadıköy, İstanbul, Türkiye",
                        "Test Sokak 1 İstanbul Kadıköy", "Test Sokak 1 Kadikoy Istanbul 34000 Turkey"]:
            with self.subTest(address=address):
                result = backfill.match(address, self.regions)
                self.assertEqual((34, 1), (result[0]["id"], result[1]["id"]))
                self.assertEqual(address, result[2])

    def test_street_mentions_and_building_separators_do_not_define_regions(self):
        for address in ["A Blok / 2. Kat - Test Sokak 1", "İstanbul Caddesi Kadıköy Apartmanı 1",
                        "Fatih Mahallesi Test Sokak İstanbul", "İstanbul / Kadıköy - ",
                        "İstanbul / Çankaya - Test Sokak 1"]:
            with self.subTest(address=address):
                self.assertIsNone(backfill.match(address, self.regions))


if __name__ == "__main__":
    unittest.main()
