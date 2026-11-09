"""Inspect product page metadata without modifying catalog data."""

import concurrent.futures
import csv
import json
from pathlib import Path
from urllib.parse import urljoin

import requests
import urllib3
from bs4 import BeautifulSoup


ROOT = Path(__file__).resolve().parent
ROWS = list(csv.DictReader((ROOT / "catalog-41-sources.csv").open(encoding="utf-8-sig", newline="")))
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


def probe(row):
    result = {"code": row["code"], "model": row["model"], "url": row["official_url"]}
    try:
        response = requests.get(row["official_url"], timeout=20, verify=not row["official_url"].startswith("https://www.furminator.com/"), headers={"User-Agent": "Mozilla/5.0 (compatible; PawMartCatalogResearch/1.0)"})
        result["status"] = response.status_code
        result["final_url"] = response.url
        if response.status_code != 200:
            return result
        soup = BeautifulSoup(response.text, "html.parser")
        result["title"] = soup.title.get_text(" ", strip=True) if soup.title else ""
        for field, property_name in (("image", "og:image"), ("description", "og:description")):
            tag = soup.find("meta", attrs={"property": property_name})
            if not tag:
                tag = soup.find("meta", attrs={"name": property_name.replace("og:", "")})
            result[field] = urljoin(response.url, tag.get("content", "")) if field == "image" and tag else (tag.get("content", "") if tag else "")
        result["image_alt"] = [image.get("alt", "") for image in soup.find_all("img", limit=10)]
        result["jsonld_products"] = []
        for tag in soup.find_all("script", attrs={"type": "application/ld+json"}):
            try:
                data = json.loads(tag.string or tag.get_text())
            except (ValueError, TypeError):
                continue
            for item in data if isinstance(data, list) else [data]:
                if isinstance(item, dict) and "Product" in str(item.get("@type", "")):
                    result["jsonld_products"].append({key: item.get(key) for key in ("name", "description", "image", "sku", "offers")})
    except requests.RequestException as exc:
        result["error"] = str(exc)
    return result


with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
    results = sorted(pool.map(probe, ROWS), key=lambda x: x["code"])

(ROOT / "catalog-41-page-probe.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
for result in results:
    print(result["code"], result.get("status", result.get("error", "error")), bool(result.get("image")), result.get("title", "")[:85].encode("ascii", "replace").decode())
