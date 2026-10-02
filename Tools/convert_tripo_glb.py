import bpy
import os
import sys
import shutil

SOURCE = "/Users/shuwen/Downloads"
OUTPUT = "/Users/shuwen/Desktop/CatChaosUnity/catastrophe/Assets/Models/Tripo/FBX"

# Desired Unity-space size in metres. Mode is either Y height or longest extent.
SPECS = {
    "book-stack": ("height", 0.34),
    "cat-tree": ("height", 1.80),
    "chair": ("height", 0.88),
    "cup": ("height", 0.10),
    "floor": ("max", 14.0),
    "flower-vase": ("height", 0.48),
    "laptop": ("max", 0.58),
    "mug": ("height", 0.11),
    "pothos-plant": ("height", 0.48),
    "shelving-unit": ("height", 3.10),
    "sofa": ("height", 0.92),
    "table": ("height", 0.76),
    "table-lamp": ("height", 0.78),
    "tablet": ("max", 0.34),
    "television": ("height", 0.82),
    "tv-cabinet": ("height", 0.92),
}

def world_bounds(objects):
    points = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        points.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
    mins = [min(p[i] for p in points) for i in range(3)]
    maxs = [max(p[i] for p in points) for i in range(3)]
    return mins, maxs

def convert(slug, mode, target):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    source = os.path.join(SOURCE, f"hi3d-{slug}_1.glb")
    bpy.ops.import_scene.gltf(filepath=source)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"No mesh found in {source}")
    mins, maxs = world_bounds(meshes)
    extents = [maxs[i] - mins[i] for i in range(3)]
    reference = extents[1] if mode == "height" else max(extents)
    scale = target / max(reference, 1e-6)
    for obj in bpy.context.scene.objects:
        if obj.parent is None:
            obj.scale = tuple(component * scale for component in obj.scale)
    bpy.context.view_layer.update()
    mins, maxs = world_bounds(meshes)
    # Put the model's bottom at y=0 and center it on x/z for predictable pivots.
    offset = (-(mins[0] + maxs[0]) * 0.5, -mins[1], -(mins[2] + maxs[2]) * 0.5)
    for obj in bpy.context.scene.objects:
        if obj.parent is None:
            obj.location.x += offset[0]
            obj.location.y += offset[1]
            obj.location.z += offset[2]
    bpy.context.view_layer.update()
    os.makedirs(OUTPUT, exist_ok=True)
    asset_name = "Tripo_" + slug.replace("-", "_").title().replace("_", "")
    texture_dir = os.path.join(OUTPUT, "Textures", asset_name)
    if os.path.isdir(texture_dir):
        shutil.rmtree(texture_dir)
    os.makedirs(texture_dir, exist_ok=True)
    for index, image in enumerate(bpy.data.images):
        if image.name == "Render Result" or image.size[0] == 0:
            continue
        safe_name = "".join(c if c.isalnum() or c in "-_" else "_" for c in image.name)
        image.filepath_raw = os.path.join(texture_dir, f"{index:02d}_{safe_name}.png")
        image.file_format = "PNG"
        try:
            image.save()
        except RuntimeError:
            image.save_render(image.filepath_raw)
    out = os.path.join(OUTPUT, asset_name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=out,
        use_selection=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False,
        object_types={"MESH", "EMPTY"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        path_mode="RELATIVE",
        embed_textures=False,
        add_leaf_bones=False,
    )
    print(f"CONVERTED {slug}: scale={scale:.6f} -> {out}")

for slug, (mode, target) in SPECS.items():
    convert(slug, mode, target)
