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
	public float averageTemp = 0.5f;
	[Range(0,1)]
	public float averagePrec = 0.5f;
	public float minkowskiLambda = 3f;

	public Image TempMap;
	public Image PrecMap;
	public Image BiomeMap;
	public bool generateNewMap = true;
	private int[] tempIntensityArray;
	private int[] precIntensityArray;

	private bool biomeGenerationFinished = false;
	//contains a list of region neighbor ids for every region
	private List<int>[] tempRegionNeighbors;
	private List<int>[] precRegionNeighbors;

	//Temp (0-2) & Prec (3-5) Colors
	private Dictionary<int, Color32> tempColors = new Dictionary<int, Color32>(){
		{0, new Color32(255, 255, 0, 255)},
		{1, new Color32(255, 153, 51, 255)},
		{2, new Color32(255, 0, 0, 255)}
	};
	private Dictionary<int, Color32> precColors = new Dictionary<int, Color32>(){
		{0, new Color32(153, 204, 255, 255)},
		{1, new Color32(0, 153, 255, 255)},
		{2, new Color32(0, 0, 255, 255)}
	};
	private bool tmp = true;

	private void Update() {
		if(generateNewMap) {
			generateNewMap = false;

			//init region neighbor arrays
			tempRegionNeighbors = new List<int>[regionAmount];
			for (int i = 0; i < regionAmount; i++) {
				tempRegionNeighbors[i] = new List<int>();
			}
			precRegionNeighbors = new List<int>[regionAmount];
			for (int i = 0; i < regionAmount; i++) {
				precRegionNeighbors[i] = new List<int>();
			}

			//generate/set intensityArrays, textures, sprites
			tempIntensityArray = calculateVoronoiDiagram(true);
			Texture2D tempTexture = GetTextureFromIntensityArray(tempIntensityArray, tempColors);
			Sprite TempSprite = Sprite.Create(tempTexture, new Rect(0, 0, imageDim.x, imageDim.y), Vector2.one * 0.5f);
			TempMap.sprite = TempSprite;

			precIntensityArray = calculateVoronoiDiagram(false);
			Texture2D precTexture = GetTextureFromIntensityArray(precIntensityArray, precColors);
			Sprite PrecSprite = Sprite.Create(precTexture, new Rect(0, 0, imageDim.x, imageDim.y), Vector2.one * 0.5f);
			PrecMap.sprite = PrecSprite;

			Texture2D biomeTexture = MergeTextures(tempTexture, precTexture);
			BiomeMap.sprite = Sprite.Create(biomeTexture, new Rect(0, 0, imageDim.x, imageDim.y), Vector2.one * 0.5f);
			biomeGenerationFinished = true;
		}
	}

	//calc voronoi diagram: array of intensity values (0-2)
	private int[] calculateVoronoiDiagram(bool isTempDiagram) {
		//roll centroids
		Vector2Int[] centroids = new Vector2Int[regionAmount];
		for(int i = 0; i < regionAmount; i++)
		{
			centroids[i] = new Vector2Int(Random.Range(0, imageDim.x), Random.Range(0, imageDim.y));
		}
		
		//determine region id of each pixel
		int[] pixelRegions = new int[imageDim.x * imageDim.y];
        int index = 0;
		for(int y = 0; y < imageDim.y; y++)
		{
			for(int x = 0; x < imageDim.x; x++)
			{
				pixelRegions[index] = GetClosestCentroidIndex(new Vector2Int(x, y), centroids, isTempDiagram);
                index++;
			}
		}

		//determine intensity ids for regions
		int[] regionIntensityIDs;
		if (isTempDiagram) {
			regionIntensityIDs = CalcRegionIntensityIDs(isTempDiagram, tempRegionNeighbors);
		} else {
			regionIntensityIDs = CalcRegionIntensityIDs(isTempDiagram, precRegionNeighbors);
		}
		
		//
		int[] pixelIntensityIDs = new int [imageDim.x * imageDim.y];
		for (int i = 0; i < pixelIntensityIDs.Length; i++) {
			int pixelIntensityID = regionIntensityIDs[pixelRegions[i]];
			pixelIntensityIDs[i] = pixelIntensityID;
		}
		return pixelIntensityIDs;
	}
	int[] CalcRegionIntensityIDs(bool calcTempDiagram, List<int>[] regionNeighbors) {
		int[] regionIntensityIDs = new int[regionAmount];
		for (int i = 0; i < regionAmount; i++) {
			regionIntensityIDs[i] = -1;
		}
		for (int i = 0; i < regionAmount; i++) {
			List<int> neighboringIntensityIDs = new List<int>();
			for (int j = 0; j < regionNeighbors[i].Count; j++) {
				int neighborID = regionNeighbors[i][j];
				if (!neighboringIntensityIDs.Contains(regionIntensityIDs[neighborID])) {
					neighboringIntensityIDs.Add(regionIntensityIDs[neighborID]);
				}
			}
			if (!neighboringIntensityIDs.Contains(0) && !neighboringIntensityIDs.Contains(2)) {
				regionIntensityIDs[i] = rollIntensityID(calcTempDiagram, 0, 3);
			} else if (!neighboringIntensityIDs.Contains(0) && neighboringIntensityIDs.Contains(2)) {
				regionIntensityIDs[i] = rollIntensityID(calcTempDiagram, 1, 3);
			} else if (neighboringIntensityIDs.Contains(0) && !neighboringIntensityIDs.Contains(2)) {
				regionIntensityIDs[i] = rollIntensityID(calcTempDiagram, 0, 2);
			} else {
				regionIntensityIDs[i] = 1;
			}
		}
		return regionIntensityIDs;
	}

	private int rollIntensityID(bool calcTempDiagram, int lowestIntensityID, int highestIntensityID) {
		float probabilityFactor;
		if(calcTempDiagram) {
			probabilityFactor = averageTemp;
		} else {
			probabilityFactor = averagePrec;
		}
		float dieValue;
		int intensityIDRange = highestIntensityID - lowestIntensityID;
		int intensityID;
		if(intensityIDRange == 3) {
			//there are 3 possible color IDs
			//calculate probabilities from probabilityFactor with quadratic functions
			float lowProbability = 2/3 * Mathf.Pow(probabilityFactor,2) - 5/3 * probabilityFactor + 1;
			float highProbability = 2/3 * Mathf.Pow(probabilityFactor,2) + 1/3 * probabilityFactor;
			dieValue = Random.Range(0f, 1f);
			if(dieValue < lowProbability)
				intensityID = 0;
			else if(dieValue >= lowProbability && dieValue <= highProbability)
				intensityID = 1;
			else
				intensityID = 2;
		} else {
			//there are 2 possible color IDs
			dieValue = Random.Range(0f, 1f);
			if(lowestIntensityID == 0) {
				//possible color IDs are 0 and 1
				if(dieValue < 1-probabilityFactor)
					intensityID = 0;
				else
					intensityID = 1;
			} else {
				//possible color IDs are 1 and 2
				if(dieValue < 1-probabilityFactor)
					intensityID = 1;
				else
					intensityID = 2;
			}
		}
		return intensityID;
	}
	int GetClosestCentroidIndex(Vector2Int pixelPos, Vector2Int[] centroids, bool calcTempDiagram)
	{
		float smallestDst = float.MaxValue;
		int nearestRegionID = 0;
		int secondNearestRegionID = 0;

		//find nearest region id
		for(int i = 0; i < centroids.Length; i++)
		{
			//float distance = (Vector2.Distance(pixelPos, centroids[i]));
			float distance = calcMinkowskiDistance(pixelPos, centroids[i], minkowskiLambda);
			if (distance < smallestDst)
			{
				nearestRegionID = i;
				smallestDst = distance;
			}
		}
		tmp = false;

		//find second nearest region id (determine neighboring regions)
		smallestDst = float.MaxValue;
		for(int i = 0; i < centroids.Length; i++)
		{
			if (i != nearestRegionID) {
				//float distance = (Vector2.Distance(pixelPos, centroids[i]));
				float distance = calcMinkowskiDistance(pixelPos, centroids[i], minkowskiLambda);
				if (distance < smallestDst) {
					secondNearestRegionID = i;
					smallestDst = distance;
				}
			}
		}

		//add neighboring region, if not already contained
		if (calcTempDiagram) {
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

	//manhatten: p=1, euclidean: p=2
	private float calcMinkowskiDistance(Vector2Int pointA, Vector2Int pointB, float lambda) {
		return Mathf.Pow(Mathf.Pow(Mathf.Abs(pointA.x-pointB.x),lambda) + Mathf.Pow(Mathf.Abs(pointA.y-pointB.y),lambda), 1/lambda);
	}

	//Textures have to be same size
	Texture2D MergeTextures(Texture2D firstTexture, Texture2D secondTexture) {
		Color[] pixelColors = new Color[imageDim.x * imageDim.y];

		int index = 0;
		for (int y = 0; y < imageDim.y; y++) {
			for (int x = 0; x < imageDim.x; x++) {
				Color firstColor = firstTexture.GetPixel(x,y);
				Color secondColor = secondTexture.GetPixel(x,y);
				pixelColors[index] = mergeColors(firstColor, secondColor, 0.5f);
				index++;
			}
		}

		Texture2D mergedTexture = GetTextureFromColorArray(pixelColors);
		return mergedTexture;
	}

	//pixelIntensities has size xSize*ySize of the texture
	//pixelIntensities has colors.size (3) distinct values
	//colors: map intensityIDs to color values
	public Texture2D GetTextureFromIntensityArray(int[] pixelIntensities, Dictionary<int, Color32> colors)
	{
		Color[] pixelColors = new Color [pixelIntensities.Length];
		for (int i = 0; i < pixelIntensities.Length; i++) {
			pixelColors[i] = colors[pixelIntensities[i]];
		}
		return GetTextureFromColorArray(pixelColors);
	}

	private Texture2D GetTextureFromColorArray(Color[] pixelColors) {
		Texture2D tex = new Texture2D(imageDim.x, imageDim.y);
		tex.filterMode = FilterMode.Point;
		tex.SetPixels(pixelColors);
		tex.Apply();
		return tex;
	}

	public Texture2D getBiomeTexture() {
		return BiomeMap.sprite.texture;
	}

	public Texture2D getTempTexture() {
		return TempMap.sprite.texture;
	}

	public Texture2D getPrecTexture() {
		return PrecMap.sprite.texture;
	}

	public bool isBiomeTextureGenerated() {
		return biomeGenerationFinished;
	}

	public Color mergeColors(Color color1, Color color2, float mergeProportions) {
		return Color.Lerp(color1, color2, Mathf.Clamp(mergeProportions, 0f, 1f));
	}
}