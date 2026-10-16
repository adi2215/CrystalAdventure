using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Tilemaps;
using System.Collections;

public class VideoToTilemap : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public RenderTexture renderTexture;
    public Tilemap tilemap;
    public Tile blackTile;
    public Tile whiteTile;

    public int width = 128;
    public int height = 96;

    private Texture2D videoFrame;
    private Tile[,] previousTiles;

    /*private void Start()
    {
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.isLooping = false;
        videoPlayer.Prepare();

        videoFrame = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        StartCoroutine(PlayVideoAsTilemap());
    }*/

    /*IEnumerator PlayVideoAsTilemap()
    {
        // Дождаться старта видео
        yield return new WaitUntil(() => videoPlayer.isPrepared);
        
        videoPlayer.Play();

        while (videoPlayer.isPlaying)
        {
            yield return new WaitForEndOfFrame();

            RenderTexture.active = renderTexture;
            videoFrame.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            videoFrame.Apply();

            DisplayFrame(videoFrame);

            // Подстрой под частоту видео, чтобы не гнаться за FPS
            yield return new WaitForSeconds(1f / 30f);
        }
    }*/

    /*private void Start()
    {
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.isLooping = false;
        videoPlayer.sendFrameReadyEvents = true;

        videoFrame = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);

        videoPlayer.frameReady += OnNewFrame;
        videoPlayer.Prepare();

        previousTiles = new Tile[width, height];

        StartCoroutine(WaitAndPlay());
    }

    IEnumerator WaitAndPlay()
    {
        yield return new WaitUntil(() => videoPlayer.isPrepared);
        videoPlayer.Play();
    }

    private void OnNewFrame(VideoPlayer source, long frameIdx)
    {
        RenderTexture.active = renderTexture;
        videoFrame.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        videoFrame.Apply();
        DisplayFrame(videoFrame);
    }


    void DisplayFrame(Texture2D frame)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int px = Mathf.FloorToInt((float)x / width * frame.width);
                int py = Mathf.FloorToInt((float)y / height * frame.height);

                Color pixel = frame.GetPixel(px, py);
                float brightness = pixel.grayscale;

                Tile newTile = brightness < 0.5f ? blackTile : whiteTile;
                if (previousTiles[x, y] != newTile)
                {
                    Vector3Int tilePos = new Vector3Int(x - width / 2, y - height / 2, 0);
                    tilemap.SetTile(tilePos, newTile);
                    previousTiles[x, y] = newTile;
                }
            }
        }
    }*/
    public Tile baseTile; // Белый универсальный тайл


    public float waveHeight = 0.3f;

    private Color[,] previousColors;
    public int cropTop = 5;
    public int cropBottom = 5;
    int croppedHeight;

    private void Start()
    {
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.isLooping = false;
        videoPlayer.sendFrameReadyEvents = true;

        videoFrame = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);

        videoPlayer.frameReady += OnNewFrame;
        videoPlayer.Prepare();

        previousColors = new Color[width, height];
        StartCoroutine(WaitAndPlay());
    }

    IEnumerator WaitAndPlay()
    {
        yield return new WaitUntil(() => videoPlayer.isPrepared);
        croppedHeight = height - cropTop - cropBottom;
        videoPlayer.Play();
    }

    void OnNewFrame(VideoPlayer source, long frameIdx)
    {
        RenderTexture.active = renderTexture;
        videoFrame.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        videoFrame.Apply();

        //UpdateTilemap(videoFrame);

        StartCoroutine(UpdateTilemap(videoFrame));
    }

    bool waveDone = false;

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            waveDone = false;
        }
    }

    IEnumerator UpdateTilemap(Texture2D frame)
    {
        if (!waveDone)
        {
            Vector2 center = new Vector2(width / 2f, croppedHeight / 2f);
            float maxDistance = Vector2.Distance(Vector2.zero, center);

            for (int y = 0; y < croppedHeight; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    float delay = distance / maxDistance * 2f;

                    int px = Mathf.FloorToInt((float)x / width * frame.width);
                    int py = Mathf.FloorToInt((float)(y + cropBottom) / height * frame.height);
                    Color pixelColor = frame.GetPixel(px, py);
                    Vector3Int tilePos = new Vector3Int(x - width / 2, y - croppedHeight / 2, 0);

                    StartCoroutine(UpdateTileDelayed(tilePos, pixelColor, x, y, delay));
                }
            }

            waveDone = true;
        }
        else
        {
            for (int y = 0; y < croppedHeight; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int px = Mathf.FloorToInt((float)x / width * frame.width);
                    int py = Mathf.FloorToInt((float)(y + cropBottom) / height * frame.height);
                    Color pixelColor = frame.GetPixel(px, py);
                    Vector3Int tilePos = new Vector3Int(x - width / 2, y - croppedHeight / 2, 0);

                    if (tilemap.GetTile(tilePos) == null)
                    {
                        tilemap.SetTile(tilePos, baseTile);
                        previousColors[x, y] = Color.clear;
                    }

                    if (previousColors[x, y] != pixelColor)
                    {
                        tilemap.SetTileFlags(tilePos, TileFlags.None);
                        tilemap.SetColor(tilePos, pixelColor);
                        previousColors[x, y] = pixelColor;
                    }
                }
            }
        }

        yield return null;
    }

    IEnumerator UpdateTileDelayed(Vector3Int tilePos, Color pixelColor, int x, int y, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (tilemap.GetTile(tilePos) == null)
        {
            tilemap.SetTile(tilePos, baseTile);
            previousColors[x, y] = Color.clear;
        }

        tilemap.SetTileFlags(tilePos, TileFlags.None);
        tilemap.SetColor(tilePos, pixelColor);
        tilemap.SetTransformMatrix(tilePos, Matrix4x4.Translate(new Vector3(0, waveHeight, 0)));

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float offsetY = Mathf.Lerp(waveHeight, 0, t);
            tilemap.SetTransformMatrix(tilePos, Matrix4x4.Translate(new Vector3(0, offsetY, 0)));
            elapsed += Time.deltaTime;
            yield return null;
        }

        tilemap.SetTransformMatrix(tilePos, Matrix4x4.identity);
        previousColors[x, y] = pixelColor;
    }


    /*void UpdateTilemap(Texture2D frame)
    {
        for (int y = 0; y < croppedHeight; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int px = Mathf.FloorToInt((float)x / width * frame.width);
                int py = Mathf.FloorToInt((float)(y + cropBottom) / height * frame.height);

                Color pixelColor = frame.GetPixel(px, py);
                Vector3Int tilePos = new Vector3Int(x - width / 2, y - croppedHeight / 2, 0);

                if (tilemap.GetTile(tilePos) == null)
                {
                    tilemap.SetTile(tilePos, baseTile);
                    previousColors[x, y] = Color.clear;
                }

                if (previousColors[x, y] != pixelColor)
                {
                    tilemap.SetTileFlags(tilePos, TileFlags.None);
                    tilemap.SetColor(tilePos, pixelColor);
                    previousColors[x, y] = pixelColor;
                }
            }
        }
    }*/

    /*bool waveDone = false;
    public float waveDuration;



    private void Start()
    {
        croppedHeight = height - cropTop - cropBottom;
        StartCoroutine(StartWave());
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            waveDone = false;
            StartCoroutine(StartWave());
        }
    }

    IEnumerator StartWave()
    {
        if (waveDone) yield break;
        waveDone = true;

        Vector2 center = new Vector2(width / 2f, croppedHeight / 2f);
        float maxDistance = Vector2.Distance(Vector2.zero, center);

        for (int y = 0; y < croppedHeight; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);
                float delay = (distance / maxDistance) * waveDuration;

                Vector3Int tilePos = new Vector3Int(x - width / 2, y - croppedHeight / 2, 0);
                StartCoroutine(SetTileWithWave(tilePos, delay));
            }
        }

        yield return null;
    }

    IEnumerator SetTileWithWave(Vector3Int tilePos, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (tilemap.GetTile(tilePos) == null)
        {
            tilemap.SetTile(tilePos, baseTile);
            tilemap.SetTileFlags(tilePos, TileFlags.None);
            tilemap.SetColor(tilePos, Color.white);
        }

        float timer = 0f;
        float waveTime = 0.5f;

        while (timer < waveTime)
        {
            timer += Time.deltaTime;
            float offsetY = Mathf.Sin((timer / waveTime) * Mathf.PI) * waveHeight;
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(0f, offsetY, 0f), Quaternion.identity, Vector3.one);
            tilemap.SetTransformMatrix(tilePos, matrix);
            yield return null;
        }

        // После волны возвращаем матрицу в норму
        tilemap.SetTransformMatrix(tilePos, Matrix4x4.identity);
    }*/
}
