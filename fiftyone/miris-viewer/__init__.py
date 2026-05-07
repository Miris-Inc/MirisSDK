import os
import urllib.request

import fiftyone as fo
import fiftyone.operators as foo
import fiftyone.operators.types as types


def _thumbnails_dir(dataset_name: str) -> str:
    return os.path.join(os.path.expanduser("~"), "fiftyone", dataset_name, "thumbnails")


def _cache_thumbnail(uuid: str, url: str, dataset_name: str) -> str | None:
    """Download thumbnail to ~/fiftyone/<dataset>/thumbnails/ and return the path."""
    if not url:
        return None
    dest_dir = _thumbnails_dir(dataset_name)
    os.makedirs(dest_dir, exist_ok=True)
    path_part = url.split("?")[0]
    ext = path_part.rsplit(".", 1)[-1] if "." in path_part else "png"
    local_path = os.path.join(dest_dir, f"{uuid}.{ext}")
    try:
        urllib.request.urlretrieve(url, local_path)
    except Exception:
        return None
    return local_path


class UpsertMirisAsset(foo.Operator):
    """Bridge operator called by the JS sync operator.

    Creates a minimal sample for a Miris asset if one does not already exist
    in the current dataset, or updates if it exists already. Also persists
    the viewer key used for this sync into dataset.info so the viewer panel
    can retrieve it later.
    """

    @property
    def config(self):
        return foo.OperatorConfig(
            name="upsert_miris_asset",
            label="Upsert Miris Asset",
            unlisted=True,
        )

    def resolve_input(self, ctx):
        inputs = types.Object()
        inputs.str("uuid", required=True)
        inputs.str("name", required=True)
        inputs.str("thumbnail", required=True)
        inputs.str("viewer_key", required=False)
        return types.Property(inputs)

    def execute(self, ctx):
        uuid = ctx.params["uuid"]
        name = ctx.params.get("name", "")
        thumbnail = ctx.params.get("thumbnail", "")
        viewer_key = ctx.params.get("viewer_key", "")
        tags: list[str] = ctx.params.get("tags", [])

        if not thumbnail:
            return {"uuid": uuid, "action": "skipped", "reason": "no thumbnail URL"}

        dataset = ctx.dataset
        if dataset is None:
            raise ValueError(
                "No dataset is currently loaded. "
                "Open a dataset in FiftyOne before syncing Miris assets."
            )

        if viewer_key:
            dataset.info["miris_viewer_key"] = viewer_key
            dataset.save()

        if "bounding_box" not in dataset.get_field_schema():
            dataset.add_sample_field(
                "bounding_box",
                fo.EmbeddedDocumentField,
                embedded_doc_type=fo.Detections,
            )

        local_path = _cache_thumbnail(uuid, thumbnail, dataset.name)
        if not local_path:
            return {"uuid": uuid, "action": "skipped", "reason": "thumbnail download failed"}

        existing: fo.Sample | None = next(
            iter(dataset.match(fo.ViewField("miris_asset_uuid") == uuid)), None
        )

        if existing is not None:
            existing["filepath"] = local_path
            existing["miris_asset_name"] = name
            existing["miris_thumbnail_url"] = thumbnail
            existing.save()
            return {"uuid": uuid, "action": "updated"}

        sample = fo.Sample(
            filepath=local_path,
            tags=tags,
            miris_asset_uuid=uuid,
            miris_asset_name=name,
            miris_thumbnail_url=thumbnail,
        )
        dataset.add_sample(sample)
        return {"uuid": uuid, "action": "created"}


def register(p):
    p.register(UpsertMirisAsset)
