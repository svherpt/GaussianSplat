import json
import numpy as np
from PIL import Image
import open3d as o3d

def load_frames(export_dir):
    with open(f"{export_dir}/frames.json") as f:
        return json.load(f)["frames"]

def quaternion_to_matrix(q):
    x, y, z, w = q
    return np.array([
        [1 - 2*(y*y + z*z),     2*(x*y - z*w),     2*(x*z + y*w)],
        [    2*(x*y + z*w), 1 - 2*(x*x + z*z),     2*(y*z - x*w)],
        [    2*(x*z - y*w),     2*(y*z + x*w), 1 - 2*(x*x + y*y)]
    ])

def backproject(depth, color, fov_deg, width, height, position, rotation):
    fx = fy = (width / 2) / np.tan(np.radians(fov_deg / 2))

    intrinsics = o3d.camera.PinholeCameraIntrinsic(width, height, fx, fy, width/2, height/2)

    color_o3d = o3d.geometry.Image((color * 255).astype(np.uint8))
    depth_o3d = o3d.geometry.Image(depth[:,:,0].astype(np.float32))

    rgbd = o3d.geometry.RGBDImage.create_from_color_and_depth(
        color_o3d, depth_o3d,
        depth_scale=1.0,
        depth_trunc=1.0,
        convert_rgb_to_intensity=False
    )

    pcd = o3d.geometry.PointCloud.create_from_rgbd_image(rgbd, intrinsics)

    pos = np.array(position)
    rot = quaternion_to_matrix(rotation)

    T = np.eye(4)
    T[:3, :3] = rot
    T[:3, 3] = pos
    pcd.transform(T)

    return np.asarray(pcd.points), np.asarray(pcd.colors)

def main():
    export_dir = "./data/simple_scene"
    frames = load_frames(export_dir)

    geometries = []
    all_points = []
    all_colors = []

    for frame in frames:
        pos = np.array(frame["position"])
        rot = quaternion_to_matrix(frame["rotation"])
        forward = rot @ np.array([0, 0, 1])

        # camera sphere
        sphere = o3d.geometry.TriangleMesh.create_sphere(radius=0.05)
        sphere.translate(pos)
        sphere.paint_uniform_color([1, 0, 0])
        geometries.append(sphere)

        # forward direction line
        points = [pos, pos + forward * 0.5]
        lines = [[0, 1]]
        line_set = o3d.geometry.LineSet()
        line_set.points = o3d.utility.Vector3dVector(points)
        line_set.lines = o3d.utility.Vector2iVector(lines)
        line_set.paint_uniform_color([0, 1, 0])
        geometries.append(line_set)

        # backproject
        color = np.array(Image.open(f"{export_dir}/{frame['colorPath']}")) / 255.0
        depth = np.array(Image.open(f"{export_dir}/{frame['depthPath']}")) / 255.0

        points_3d, colors_3d = backproject(
            depth, color,
            frame["fov"], frame["width"], frame["height"],
            frame["position"], frame["rotation"]
        )
        all_points.append(points_3d)
        all_colors.append(colors_3d)

    points = np.concatenate(all_points, axis=0)
    colors = np.concatenate(all_colors, axis=0)

    pcd = o3d.geometry.PointCloud()
    pcd.points = o3d.utility.Vector3dVector(points)
    pcd.colors = o3d.utility.Vector3dVector(colors)
    geometries.append(pcd)

    axis = o3d.geometry.TriangleMesh.create_coordinate_frame(size=0.5)
    geometries.append(axis)

    o3d.visualization.draw_geometries(geometries)

if __name__ == "__main__":
    main()