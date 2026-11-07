"""Gather product photo candidates from known model pages for visual review."""

import csv
import io
import json
from pathlib import Path
from urllib.parse import urljoin

import requests
import urllib3
from bs4 import BeautifulSoup
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent
OUT = ROOT / "catalog-41-image-review"
OUT.mkdir(exist_ok=True)
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
rows = list(csv.DictReader((ROOT / "catalog-41-sources.csv").open(encoding="utf-8-sig", newline="")))
probe = {x["code"]: x for x in json.loads((ROOT / "catalog-41-page-probe.json").read_text(encoding="utf-8"))}
royal = {
    "A02": "https://marspetcareaprimocdn.petcare.global/6e85bfb2-2a80-4d09-800d-b3aa00a4ebfc/6e85bfb2-2a80-4d09-800d-b3aa00a4ebfc_DownloadAsJpg.jpg",
    "A05": "https://marspetcareaprimocdn.petcare.global/c2698e01-24b8-4434-b780-b2490009a85b/c2698e01-24b8-4434-b780-b2490009a85b_DownloadAsJpg.jpg",
    "A06": "https://marspetcareaprimocdn.petcare.global/174a77ba-09ae-4328-acbb-b1ef00132462/174a77ba-09ae-4328-acbb-b1ef00132462_DownloadAsJpg.jpg",
    "A07": "https://marspetcareaprimocdn.petcare.global/fa1241ac-7528-4e00-b045-b2490009a695/fa1241ac-7528-4e00-b045-b2490009a695_DownloadAsJpg.jpg",
    "A08": "https://cdn.royalcanin-weshare-online.io/SCEua2QBaxEApS7Lk_vW/v176/16-sterilised-37-b1-ru",
}
exact_images = {
    "C03": "https://uk.virbac.com/var/site/storage/images/4/8/9/2/22984-1-eng-GB/f24ba0b83054-309623_Packshot_Enzymatic-Toothpaste_70g_face.png",
}
special_pages = {
    "C03": "https://uk.virbac.com/products/enzymatic-toothpaste-for-dogs-cats?category=128",
    "E05": "https://petkitgermany.com/products/puramax-2-selbstreinigende-katzentoilette",
}


def first_matching_image(row, soup, url):
    code = row["code"]
    if code.startswith("C") and row["brand"] == "FURminator":
        images = [x for x in soup.select("img") if "spectrumbrands.com" in (x.get("src") or "") and "product-images" in (x.get("src") or "")]
        image = next((x for x in images if "_01_" in x.get("alt", "") or "Front Render" in x.get("alt", "")), images[0] if images else None)
        return urljoin(url, image.get("src")) if image else None
    if code == "A03":
        image = next((x for x in soup.select("img") if "9310022866203.png" in (x.get("src") or "")), None)
        return urljoin(url, image.get("src")) if image else None
    if code == "A04":
        image = next((x for x in soup.select("img") if "upload/product_gallery/" in (x.get("src") or "")), None)
        return urljoin(url, image.get("src")) if image else None
    if code == "C03":
        meta = soup.find("meta", attrs={"property": "og:image"})
        return urljoin(url, meta.get("content")) if meta else None
    if code == "C07":
        image = next((x for x in soup.select("img") if "CET_Dual_Ended_Toothbrush" in (x.get("src") or x.get("data-src") or "")), None)
        return urljoin(url, image.get("src") or image.get("data-src")) if image else None
    return probe[code].get("image")


manifest = []
for row in rows:
    code = row["code"]
    page_url = special_pages.get(code, row["official_url"])
    image_url = exact_images.get(code) or royal.get(code) or probe[code].get("image")
    if code in {"A03", "A04", "C07", "E05"} or row["brand"] == "FURminator":
        try:
            page = requests.get(page_url, timeout=20, verify=not page_url.startswith("https://www.furminator.com/"), headers={"User-Agent": "Mozilla/5.0"})
            page.raise_for_status()
            soup = BeautifulSoup(page.text, "html.parser")
            image_url = first_matching_image(row, soup, page.url)
            if code == "E05":
                meta = soup.find("meta", attrs={"property": "og:image"})
                image_url = urljoin(page.url, meta.get("content")) if meta else None
        except requests.RequestException as exc:
            print(code, "page", str(exc)[:100])
    if not image_url:
        manifest.append({"code": code, "source_url": page_url, "image_url": None, "result": "NO_CANDIDATE"})
        continue
    try:
        image_response = requests.get(image_url, timeout=20, headers={"User-Agent": "Mozilla/5.0", "Referer": page_url})
        image_response.raise_for_status()
        image = Image.open(io.BytesIO(image_response.content))
        image.load()
        if min(image.size) < 180:
            raise ValueError(f"small image {image.size}")
        if image.mode == "RGBA" or "transparency" in image.info:
            background = Image.new("RGBA", image.size, "white")
            background.alpha_composite(image.convert("RGBA"))
            image = background.convert("RGB")
        else:
            image = image.convert("RGB")
        path = OUT / f"{code}.jpg"
        image.save(path, quality=90)
        manifest.append({"code": code, "source_url": page_url, "image_url": image_url, "path": str(path.relative_to(ROOT)), "size": image.size, "result": "CANDIDATE"})
    except Exception as exc:
        manifest.append({"code": code, "source_url": page_url, "image_url": image_url, "result": "FAILED", "error": str(exc)[:160]})

(ROOT / "catalog-41-image-candidates.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
valid = [x for x in manifest if x["result"] == "CANDIDATE"]
sheet = Image.new("RGB", (1000, ((len(valid) + 4) // 5) * 245), "white")
draw = ImageDraw.Draw(sheet)
for position, entry in enumerate(valid):
    x, y = (position % 5) * 200, (position // 5) * 245
    with Image.open(ROOT / entry["path"]) as image:
        image.thumbnail((180, 185))
        sheet.paste(image, (x + (200 - image.width) // 2, y + 4 + (185 - image.height) // 2))
    draw.text((x + 5, y + 193), f"{entry['code']} {entry['size']}", fill="black")
sheet.save(OUT / "contact-sheet-all.jpg", quality=90)
print("candidates", len(valid), "missing", [x["code"] for x in manifest if x["result"] != "CANDIDATE"])
