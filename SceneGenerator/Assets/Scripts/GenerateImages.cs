using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;

public class GenerateImages : MonoBehaviour
{
    public GameObject center;
    public Camera cam;

    [Header("Camera Settings")]
    public int numPhotos = 12;
    public float radius = 5f;
    public float maxHeight = 2f;

    [Header("Image Settings")]
    public int imageWidth = 512;
    public int imageHeight = 512;

    [Header("Preview")]
    public Texture2D previewColor;
    public Texture2D previewDepth;

    public List<Vector3> cameraPositions;

    Texture2D RenderColor()
    {
        cam.aspect = (float)imageWidth / imageHeight;

        RenderTexture rt = RenderTexture.GetTemporary(
            imageWidth,
            imageHeight,
            24,
            RenderTextureFormat.ARGB32
        );

        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;

        Texture2D tex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);

        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        RenderTexture.ReleaseTemporary(rt);

        return tex;
    }

    Texture2D RenderDistance()
    {
        cam.aspect = (float)imageWidth / imageHeight;

        Texture2D tex = new Texture2D(imageWidth, imageHeight, TextureFormat.RFloat, false);

        float halfFovRad = cam.fieldOfView * Mathf.Deg2Rad / 2f;
        float tanHalfFov = Mathf.Tan(halfFovRad);
        float aspect = (float)imageWidth / imageHeight;

        for (int y = 0; y < imageHeight; y++)
        {
            for (int x = 0; x < imageWidth; x++)
            {
                float nx = ((x + 0.5f) / imageWidth * 2f - 1f) * aspect * tanHalfFov;
                float ny = ((y + 0.5f) / imageHeight * 2f - 1f) * tanHalfFov;

                Vector3 dirLocal = new Vector3(nx, ny, 1f).normalized;
                Vector3 dirWorld = cam.transform.TransformDirection(dirLocal);

                float distance = cam.farClipPlane;

                if (Physics.Raycast(cam.transform.position, dirWorld, out RaycastHit hit, cam.farClipPlane))
                {
                    if (hit.distance >= cam.nearClipPlane)
                    {
                        distance = hit.distance;
                    }
                }

                tex.SetPixel(x, y, new Color(distance, 0, 0, 1));
            }
        }

        tex.Apply();

        float sample = tex.GetPixel(imageWidth / 2, imageHeight / 2).r;
        Debug.Log($"Distance sample at center: {sample}");

        return tex;
    }

    static float[] MatrixToArray(Matrix4x4 m)
    {
        return new float[]
        {
            m.m00, m.m01, m.m02, m.m03,
            m.m10, m.m11, m.m12, m.m13,
            m.m20, m.m21, m.m22, m.m23,
            m.m30, m.m31, m.m32, m.m33,
        };
    }

    public void Generate()
    {
        string ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        string outDir = Path.Combine(Application.dataPath, "../Export", ts);

        Directory.CreateDirectory(Path.Combine(outDir, "color"));
        Directory.CreateDirectory(Path.Combine(outDir, "depth"));

        List<FrameData> frames = new List<FrameData>();

        for (int i = 0; i < cameraPositions.Count; i++)
        {
            Vector3 pos = cameraPositions[i];

            cam.transform.position = pos;
            cam.transform.LookAt(center.transform);

            Matrix4x4 camToWorld = cam.transform.localToWorldMatrix;

            Texture2D color = RenderColor();
            Texture2D depth = RenderDistance();

            string colorFile = $"color/frame_{i:D4}.png";
            string depthFile = $"depth/frame_{i:D4}.exr";

            File.WriteAllBytes(
                Path.Combine(outDir, colorFile),
                color.EncodeToPNG()
            );

            File.WriteAllBytes(
                Path.Combine(outDir, depthFile),
                depth.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat)
            );

            frames.Add(new FrameData
            {
                index = i,
                colorPath = colorFile,
                depthPath = depthFile,
                position = new float[] { pos.x, pos.y, pos.z },
                cameraToWorld = MatrixToArray(camToWorld),
                fov = cam.fieldOfView,
                nearClip = cam.nearClipPlane,
                farClip = cam.farClipPlane,
                width = imageWidth,
                height = imageHeight,
            });

            previewColor = color;

            Texture2D depthPreview = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
            for (int y = 0; y < imageHeight; y++)
                for (int x = 0; x < imageWidth; x++)
                {
                    float d = depth.GetPixel(x, y).r;
                    float normalized = d / cam.farClipPlane;
                    depthPreview.SetPixel(x, y, new Color(normalized, normalized, normalized));
                }
            depthPreview.Apply();
            previewDepth = depthPreview;
        }

        string json = JsonUtility.ToJson(new FrameList { frames = frames }, true);

        File.WriteAllText(Path.Combine(outDir, "frames.json"), json);

        Debug.Log($"Exported {frames.Count} frames to {outDir}");
    }

    void OnValidate()
    {
        UpdateCameraPositions();
    }

    void UpdateCameraPositions()
    {
        cameraPositions = new List<Vector3>();

        for (int i = 0; i < numPhotos; i++)
        {
            float a = i * Mathf.PI * 2f / numPhotos;

            cameraPositions.Add(new Vector3(
                Mathf.Cos(a) * radius,
                UnityEngine.Random.Range(0.5f, maxHeight),
                Mathf.Sin(a) * radius
            ));
        }
    }

    void OnDrawGizmos()
    {
        if (center == null || cameraPositions == null)
            return;

        Gizmos.color = Color.yellow;

        foreach (Vector3 p in cameraPositions)
        {
            Gizmos.DrawSphere(p, 0.1f);
            Gizmos.DrawLine(p, center.transform.position);
        }
    }
}

[Serializable]
public class FrameData
{
    public int index;
    public string colorPath;
    public string depthPath;
    public float[] position;
    public float[] cameraToWorld;
    public float fov;
    public float nearClip;
    public float farClip;
    public int width;
    public int height;
}

[Serializable]
public class FrameList
{
    public List<FrameData> frames;
}