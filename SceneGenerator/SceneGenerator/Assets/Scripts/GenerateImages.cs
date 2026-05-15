using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GenerateImages : MonoBehaviour
{
    public GameObject center;
    public Camera cam;
    [Header("Camera Settings")]
    public int numPhotos;
    public float radius;
    public float height;
    [Header("Image Settings")]
    public int imageWidth;
    public int imageHeight;
    public float depthPreviewMin;
    public float depthPreviewMax;
    [Header("Preview")]
    public Texture2D previewImage;
    public Texture2D previewImageDepth;
    public List<Vector3> cameraPositions;

    void Start() { }

    public (Texture2D color, Texture2D depth) Capture(Vector3 position)
    {
        cam.transform.position = position;
        cam.transform.LookAt(center.transform);

        RenderTexture colorRT = new RenderTexture(imageWidth, imageHeight, 24);
        cam.targetTexture = colorRT;
        cam.Render();
        Texture2D colorTex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        RenderTexture.active = colorRT;
        colorTex.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        colorTex.Apply();
        colorRT.Release();
        RenderTexture.active = null;

        Texture2D depthTex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        Color[] depthPixels = new Color[imageWidth * imageHeight];
        for (int y = 0; y < imageHeight; y++)
        {
            for (int x = 0; x < imageWidth; x++)
            {
                Ray ray = cam.ScreenPointToRay(new Vector3(x, y, 0));
                float normalized = 0f;
                if (Physics.Raycast(ray, out RaycastHit hit))
                    normalized = Mathf.InverseLerp(depthPreviewMax, depthPreviewMin, hit.distance);
                depthPixels[y * imageWidth + x] = new Color(normalized, normalized, normalized);
            }
        }
        depthTex.SetPixels(depthPixels);
        depthTex.Apply();

        return (colorTex, depthTex);
    }

    public void Generate()
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string outputDir = Path.Combine(Application.dataPath, "../Export", timestamp);
        string colorDir = Path.Combine(outputDir, "color");
        string depthDir = Path.Combine(outputDir, "depth");
        Directory.CreateDirectory(colorDir);
        Directory.CreateDirectory(depthDir);

        var frames = new List<FrameData>();

        for (int i = 0; i < cameraPositions.Count; i++)
        {
            Vector3 pos = cameraPositions[i];
            (Texture2D color, Texture2D depth) = Capture(pos);

            string colorFile = $"color/frame_{i:D4}.png";
            string depthFile = $"depth/frame_{i:D4}.png";

            File.WriteAllBytes(Path.Combine(outputDir, colorFile), color.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(outputDir, depthFile), depth.EncodeToPNG());

            frames.Add(new FrameData
            {
                index = i,
                colorPath = colorFile,
                depthPath = depthFile,
                position = new float[] { pos.x, pos.y, pos.z },
                rotation = new float[]
                {
                    cam.transform.rotation.x,
                    cam.transform.rotation.y,
                    cam.transform.rotation.z,
                    cam.transform.rotation.w
                },
                fov = cam.fieldOfView,
                width = imageWidth,
                height = imageHeight
            });

            previewImage = color;
            previewImageDepth = depth;
        }

        string json = JsonUtility.ToJson(new FrameList { frames = frames }, true);
        File.WriteAllText(Path.Combine(outputDir, "frames.json"), json);

        Debug.Log($"Exported {frames.Count} frames to {outputDir}");
    }

    private void OnValidate() { updateCameraPositions(); }

    private void updateCameraPositions()
    {
        cameraPositions = new List<Vector3>();
        for (int i = 0; i < numPhotos; i++)
        {
            float angle = i * Mathf.PI * 2 / numPhotos;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = height;
            cameraPositions.Add(new Vector3(x, y, z));
        }
    }

    private void OnDrawGizmos()
    {
        foreach (Vector3 pos in cameraPositions)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pos, 0.1f);
            Vector3 direction = (center.transform.position - pos).normalized;
            Gizmos.DrawLine(pos, pos + direction);
        }
    }
}

[System.Serializable]
public class FrameData
{
    public int index;
    public string colorPath;
    public string depthPath;
    public float[] position;
    public float[] rotation;
    public float fov;
    public int width;
    public int height;
}

[System.Serializable]
public class FrameList
{
    public List<FrameData> frames;
}