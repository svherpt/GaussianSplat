import json
import numpy as np
import os

os.environ["OPENCV_IO_ENABLE_OPENEXR"] = "1"

import cv2
import open3d as o3d


def load_depth_exr(path):
    img = cv2.imread(path, cv2.IMREAD_UNCHANGED)

    if img is None:
        raise FileNotFoundError(path)

    if img.ndim == 3:
        depth = img[..., 0]
    else:
        depth = img

    return depth.astype(np.float32)


def load_color(path):
    img = cv2.imread(path)

    if img is None:
        raise FileNotFoundError(path)

    img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)

    return img.astype(np.float32) / 255.0


def backproject(depth, color, fov_deg, cam_to_world, near_clip, far_clip):
    h, w = depth.shape

    # Mask out missing hits (far plane) and behind near clip
    mask = (depth > near_clip) & (depth < far_clip * 0.9999)

    fy = (h * 0.5) / np.tan(np.radians(fov_deg) * 0.5)
    fx = fy

    cx = w * 0.5
    cy = h * 0.5

    u, v = np.meshgrid(np.arange(w), np.arange(h))

    # Pixel center offset to match Unity's (x + 0.5) convention
    uc = u[mask] + 0.5
    vc = v[mask] + 0.5

    # X right, Y up (flip v), Z forward to match Unity camera space
    x = (uc - cx) / fx
    y = -(vc - cy) / fy
    z = np.ones_like(x)

    dirs = np.stack([x, y, z], axis=-1)
    dirs /= np.linalg.norm(dirs, axis=1, keepdims=True)

    d = depth[mask]

    pts_cam = np.concatenate([
        dirs * d[:, None],
        np.ones((len(d), 1))
    ], axis=1)

    pts_world = (cam_to_world @ pts_cam.T).T[:, :3]

    return pts_world, color[mask]


def main():
    export_dir = "./data/simple_scene"

    with open(f"{export_dir}/frames.json") as f:
        frames = json.load(f)["frames"]

    geoms = [
        o3d.geometry.TriangleMesh.create_coordinate_frame(size=1.0)
    ]

    for frame in frames:
        depth = load_depth_exr(f"{export_dir}/{frame['depthPath']}")
        color = load_color(f"{export_dir}/{frame['colorPath']}")

        c2w = np.array(frame["cameraToWorld"], dtype=np.float64).reshape(4, 4)

        near_clip = frame["nearClip"]
        far_clip  = frame["farClip"]

        valid = depth[(depth > near_clip) & (depth < far_clip * 0.9999)]
        print(
            f"Frame {frame['index']}: "
            f"{valid.min():.3f}m .. {valid.max():.3f}m  "
            f"({len(valid)} valid pixels)"
        )

        pts, cols = backproject(
            depth, color, frame["fov"], c2w, near_clip, far_clip
        )

        pcd = o3d.geometry.PointCloud()
        pcd.points = o3d.utility.Vector3dVector(pts)
        pcd.colors = o3d.utility.Vector3dVector(cols)
        geoms.append(pcd)

        marker = o3d.geometry.TriangleMesh.create_sphere(radius=0.05)
        marker.translate(np.array(frame["position"]))
        marker.paint_uniform_color([1, 0, 0])
        geoms.append(marker)

    o3d.visualization.draw_geometries(geoms)


if __name__ == "__main__":
    main()