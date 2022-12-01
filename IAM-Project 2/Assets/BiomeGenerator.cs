using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//credit for voronoi generator: UpGames: "Voronoi diagram tutorial in Unity3D(C#)" (https://www.youtube.com/watch?v=EDv69onIETk)
public class BiomeGenerator : MonoBehaviour
{
    public Vector2Int imageDim;
	public int regionAmount;
	public Image TemperatureMap;
	public Image PrecipitationMap;

	//Temperature (0-2) & Precipitation (3-5) Colors
	private Color32[] diagramColors = {
		new Color32(255, 255, 255, 255),
		new Color32(51, 204, 51, 255),
		new Color32(255, 153, 51, 255),
		new Color32(102, 204, 255, 255),
		new Color32(0, 102, 204, 255),
		new Color32(0, 0, 255, 255),
	};

	//contains a list of region neighbor ids for every region
	private List<int>[] tempRegionNeighbors;
	private List<int>[] precRegionNeighbors;
	private void Start()
	{
		//init region neighbor arrays of lists
		tempRegionNeighbors = new List<int>[regionAmount];
		for (int i = 0; i < regionAmount; i++) {
			tempRegionNeighbors[i] = new List<int>();
		}

		precRegionNeighbors = new List<int>[regionAmount];
		for (int i = 0; i < regionAmount; i++) {
			precRegionNeighbors[i] = new List<int>();
		}

		Sprite TemperatureMapSprite = Sprite.Create(GetDiagram(true), new Rect(0, 0, imageDim.x, imageDim.y), Vector2.one * 0.5f);
        TemperatureMap.sprite = TemperatureMapSprite;

		Sprite PrecipitationMapSprite = Sprite.Create(GetDiagram(false), new Rect(0, 0, imageDim.x, imageDim.y), Vector2.one * 0.5f);
        PrecipitationMap.sprite = PrecipitationMapSprite;
	}
	Texture2D GetDiagram(bool calcTemperatureDiagram)
	{
		Vector2Int[] centroids = new Vector2Int[regionAmount];
		Color[] regions = new Color[regionAmount];
		for(int i = 0; i < regionAmount; i++)
		{
			centroids[i] = new Vector2Int(Random.Range(0, imageDim.x), Random.Range(0, imageDim.y));
			int regionColorID;
			if(calcTemperatureDiagram) {
				regionColorID = Random.Range(0, 3);
			} else {
				regionColorID = Random.Range(3, 6);
			}
			regions[i] = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);//diagramColors[regionColorID];
		}
		Color[] pixelColors = new Color[imageDim.x * imageDim.y];

        int index = 0;
		for(int y = 0; y < imageDim.y; y++)
		{
			for(int x = 0; x < imageDim.x; x++)
			{
				pixelColors[index] = regions[GetClosestCentroidIndex(new Vector2Int(x, y), centroids, calcTemperatureDiagram)];
                index++;
			}
		}
		return GetImageFromColorArray(pixelColors);
	}
	int GetClosestCentroidIndex(Vector2Int pixelPos, Vector2Int[] centroids, bool calcTemperatureDiagram)
	{
		float smallestDst = float.MaxValue;
		int nearestRegionID = 0;
		int secondNearestRegionID = 0;

		//find nearest region id
		for(int i = 0; i < centroids.Length; i++)
		{
			float distance = (Vector2.Distance(pixelPos, centroids[i]));
			if (distance < smallestDst)
			{
				nearestRegionID = i;
				smallestDst = distance;
			}
		}

		//find second nearest region id (determine neighboring regions)
		smallestDst = float.MaxValue;
		for(int i = 0; i < centroids.Length; i++)
		{
			if (i != nearestRegionID) {
				float distance = (Vector2.Distance(pixelPos, centroids[i]));
				if (distance < smallestDst) {
					secondNearestRegionID = i;
					smallestDst = distance;
				}
			}
		}

		//add neighboring region, if not already contained
		if (calcTemperatureDiagram) {
			if (!tempRegionNeighbors[nearestRegionID].Contains(secondNearestRegionID)) {
				tempRegionNeighbors[nearestRegionID].Add(secondNearestRegionID);
			}
		} else {
			if (!precRegionNeighbors[nearestRegionID].Contains(secondNearestRegionID)) {
				precRegionNeighbors[nearestRegionID].Add(secondNearestRegionID);
			}
		}
		return nearestRegionID;
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