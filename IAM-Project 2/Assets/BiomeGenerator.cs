using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//credit: UpGames: "Voronoi diagram tutorial in Unity3D(C#)" (https://www.youtube.com/watch?v=EDv69onIETk)
public class BiomeGenerator : MonoBehaviour
{
    public Vector2Int imageDim;
	public int regionAmount;
    public Image BiomeMap;
	private void Start()
	{
		Sprite BiomeMapSprite = Sprite.Create(GetDiagram(), new Rect(0, 0, imageDim.x, imageDim.y), Vector2.one * 0.5f);
        BiomeMap.sprite = BiomeMapSprite;
	}
	Texture2D GetDiagram()
	{
		Vector2Int[] centroids = new Vector2Int[regionAmount];
		Color[] regions = new Color[regionAmount];
		for(int i = 0; i < regionAmount; i++)
		{
			centroids[i] = new Vector2Int(Random.Range(0, imageDim.x), Random.Range(0, imageDim.y));
			regions[i] = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);
		}
		Color[] pixelColors = new Color[imageDim.x * imageDim.y];

        int index = 0;
		for(int y = 0; y < imageDim.y; y++)
		{
			for(int x = 0; x < imageDim.x; x++)
			{
				pixelColors[index] = regions[GetClosestCentroidIndex(new Vector2Int(x, y), centroids)];
                index++;
			}
		}
		return GetImageFromColorArray(pixelColors);
	}
	int GetClosestCentroidIndex(Vector2Int pixelPos, Vector2Int[] centroids)
	{
		float smallestDst = float.MaxValue;
		int index = 0;
		for(int i = 0; i < centroids.Length; i++)
		{
			if (Vector2.Distance(pixelPos, centroids[i]) < smallestDst)
			{
				smallestDst = Vector2.Distance(pixelPos, centroids[i]);
				index = i;
			}
		}
		return index;
	}
	Texture2D GetImageFromColorArray(Color[] pixelColors)
	{
		Texture2D tex = new Texture2D(imageDim.x, imageDim.y);
		tex.filterMode = FilterMode.Point;
		tex.SetPixels(pixelColors);
		tex.Apply();
		return tex;
	}
}
