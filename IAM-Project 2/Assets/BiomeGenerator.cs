using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//credit for voronoi generator: UpGames: "Voronoi diagram tutorial in Unity3D(C#)" (https://www.youtube.com/watch?v=EDv69onIETk)
public class BiomeGenerator : MonoBehaviour
{
    public Vector2Int imageDim;
	public int regionAmount;
	[Range(0,1)]
	public float averageTemperature = 0.5f;
	[Range(0,1)]
	public float averagePrecipitation = 0.5f;

	public Image TemperatureMap;
	public Image PrecipitationMap;
	public Image BiomeMap;
	public bool generateNewMap = true;

	//Temperature (0-2) & Precipitation (3-5) Colors
	private Color32[] temperatureColors = {
		new Color32(255, 255, 0, 255),
		new Color32(255, 153, 51, 255),
		new Color32(255, 0, 0, 255)
	};
	private Color32[] precipitationColors = {
		new Color32(153, 204, 255, 255),
		new Color32(0, 153, 255, 255),
		new Color32(0, 0, 255, 255)
	};

	//contains a list of region neighbor ids for every region
	private List<int>[] tempSprite_regionNeighbors;
	private List<int>[] precSprite_regionNeighbors;
	private void Update() {
		if(generateNewMap) {
			generateNewMap = false;
			//init region neighbor arrays of lists
			tempSprite_regionNeighbors = new List<int>[regionAmount];
			for (int i = 0; i < regionAmount; i++) {
				tempSprite_regionNeighbors[i] = new List<int>();
			}

			precSprite_regionNeighbors = new List<int>[regionAmount];
			for (int i = 0; i < regionAmount; i++) {
				precSprite_regionNeighbors[i] = new List<int>();
			}

			Sprite TemperatureMapSprite = Sprite.Create(GetDiagram(true), new Rect(0, 0, imageDim.x, imageDim.y), Vector2.one * 0.5f);
			TemperatureMap.sprite = TemperatureMapSprite;

			Sprite PrecipitationMapSprite = Sprite.Create(GetDiagram(false), new Rect(0, 0, imageDim.x, imageDim.y), Vector2.one * 0.5f);
			PrecipitationMap.sprite = PrecipitationMapSprite;

			BiomeMap.sprite = MergeSprites(TemperatureMapSprite, PrecipitationMapSprite);
		}
	}
	//Sprites have to be same size
	Sprite MergeSprites(Sprite firstSprite, Sprite secondSprite) {
		Texture2D firstTexture = firstSprite.texture;
		Texture2D secondTexture = secondSprite.texture;
		Color[] pixelColors = new Color[imageDim.x * imageDim.y];

		int index = 0;
		for (int y = 0; y < imageDim.y; y++) {
			for (int x = 0; x < imageDim.x; x++) {
				Color firstColor = firstTexture.GetPixel(x,y);
				Color secondColor = secondTexture.GetPixel(x,y);
				pixelColors[index] = Color.Lerp(firstColor, secondColor, 0.5f);
				index++;
			}
		}

		Texture2D mergedTexture = GetImageFromColorArray(pixelColors);
		Sprite mergedSprite = Sprite.Create(mergedTexture, new Rect(0, 0, imageDim.x, imageDim.y), Vector2.one * 0.5f);
		return mergedSprite;
	}
	Texture2D GetDiagram(bool calcTemperatureDiagram)
	{
		Vector2Int[] centroids = new Vector2Int[regionAmount];
		for(int i = 0; i < regionAmount; i++)
		{
			centroids[i] = new Vector2Int(Random.Range(0, imageDim.x), Random.Range(0, imageDim.y));
		}

		int[] pixelRegions = new int[imageDim.x * imageDim.y];
        int index = 0;
		for(int y = 0; y < imageDim.y; y++)
		{
			for(int x = 0; x < imageDim.x; x++)
			{
				pixelRegions[index] = GetClosestCentroidIndex(new Vector2Int(x, y), centroids, calcTemperatureDiagram);
                index++;
			}
		}

		int[] regionColorIDs;
		if (calcTemperatureDiagram) {
			regionColorIDs = CalcRegionColorIDs(calcTemperatureDiagram, tempSprite_regionNeighbors);
		} else {
			regionColorIDs = CalcRegionColorIDs(calcTemperatureDiagram, precSprite_regionNeighbors);
		}

		Color[] pixelColors = new Color[imageDim.x * imageDim.y];
		for (int i = 0; i < pixelColors.Length; i++) {
			int pixelColorID = regionColorIDs[pixelRegions[i]];
			if (calcTemperatureDiagram) {
				pixelColors[i] = temperatureColors[pixelColorID];
			} else {
				pixelColors[i] = precipitationColors[pixelColorID];
			}
		}
		return GetImageFromColorArray(pixelColors);
	}
	int[] CalcRegionColorIDs(bool calcTemperatureDiagram, List<int>[] regionNeighbors) {
		int[] regionColorIDs = new int[regionAmount];
		for (int i = 0; i < regionAmount; i++) {
			regionColorIDs[i] = -1;
		}
		for (int i = 0; i < regionAmount; i++) {
			List<int> neighboringColorIDs = new List<int>();
			for (int j = 0; j < regionNeighbors[i].Count; j++) {
				int neighborID = regionNeighbors[i][j];
				if (!neighboringColorIDs.Contains(regionColorIDs[neighborID])) {
					neighboringColorIDs.Add(regionColorIDs[neighborID]);
				}
			}
			if (!neighboringColorIDs.Contains(0) && !neighboringColorIDs.Contains(2)) {
				regionColorIDs[i] = rollColorID(calcTemperatureDiagram, 0, 3);
			} else if (!neighboringColorIDs.Contains(0) && neighboringColorIDs.Contains(2)) {
				regionColorIDs[i] = rollColorID(calcTemperatureDiagram, 1, 3);
			} else if (neighboringColorIDs.Contains(0) && !neighboringColorIDs.Contains(2)) {
				regionColorIDs[i] = rollColorID(calcTemperatureDiagram, 0, 2);
			} else {
				regionColorIDs[i] = 1;
			}
		}
		return regionColorIDs;
	}

	private int rollColorID(bool calcTemperatureDiagram, int lowestColorID, int highestColorID) {
		float probabilityFactor;
		if(calcTemperatureDiagram) {
			probabilityFactor = averageTemperature;
		} else {
			probabilityFactor = averagePrecipitation;
		}
		float dieValue;
		int colorIDRange = highestColorID - lowestColorID;
		int colorID;
		if(colorIDRange == 3) {
			//there are 3 possible color IDs
			//calculate probabilities from probabilityFactor with quadratic functions
			float lowProbability = 2/3 * Mathf.Pow(probabilityFactor,2) - 5/3 * probabilityFactor + 1;
			float highProbability = 2/3 * Mathf.Pow(probabilityFactor,2) + 1/3 * probabilityFactor;
			dieValue = Random.Range(0f, 1f);
			if(dieValue < lowProbability)
				colorID = 0;
			else if(dieValue >= lowProbability && dieValue <= highProbability)
				colorID = 1;
			else
				colorID = 2;
		} else {
			//there are 2 possible color IDs
			dieValue = Random.Range(0f, 1f);
			if(lowestColorID == 0) {
				//possible color IDs are 0 and 1
				if(dieValue < 1-probabilityFactor)
					colorID = 0;
				else
					colorID = 1;
			} else {
				//possible color IDs are 1 and 2
				if(dieValue < 1-probabilityFactor)
					colorID = 1;
				else
					colorID = 2;
			}
		}
		return colorID;
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
			if (!tempSprite_regionNeighbors[nearestRegionID].Contains(secondNearestRegionID)) {
				tempSprite_regionNeighbors[nearestRegionID].Add(secondNearestRegionID);
			}
		} else {
			if (!precSprite_regionNeighbors[nearestRegionID].Contains(secondNearestRegionID)) {
				precSprite_regionNeighbors[nearestRegionID].Add(secondNearestRegionID);
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