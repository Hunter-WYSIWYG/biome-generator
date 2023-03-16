using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//credit for voronoi generator: UpGames: "Voronoi diagram tutorial in Unity3D(C#)" (https://www.youtube.com/watch?v=EDv69onIETk)
public class BiomeGenerator : MonoBehaviour
{
	public bool generateNewMap = true;
	[Header("Local Minimum Centroids")]
	public int localMinimumArea;
	public bool useMinimaForCentroids;
	public bool showLocalMinima;
	[Header("Random Centroids")]
	public int regionAmount;
	[Header("Map Parameter")]
	[Range(0,1)]
	public float averageTemp = 0.5f;
	[Range(0,1)]
	public float averagePrec = 0.5f;
	public float minkowskiLambda = 3f;
	//upper x percent of terrain that have a colder temperature
	[Range(0f,1f)]
	public float realitveColdHeightsLimit = 0f;
	//maximum distance in mesh-nodes around waters that is still affected by higher humidity
	public int humiditySpread = 0;
	[Header("Sprite Processing")]
	[Range(0f,1f)]
	//eliminate regions that have a smaller size than width*height*eliminationSizeFactor
	public float eliminationSizeFactor = 0.001f;
	public bool biome_eliminateSmallRegions = false;
	public bool biome_openSprite = false;
	public bool biome_mergeTempAndPrec = false;
	public enum WizardState {BiomeView, TempView, PrecView, TerrainView};
	[Header("View State")]
	public WizardState wizardState = WizardState.TempView;
	[Header("Dependencies")]
	public TerrainGenerator terrainGenerator;
	public GameObject tempSprite;
	public GameObject precSprite;
	public GameObject biomeSprite;
	public GameObject spriteCam;
	public GameObject terrainCam;
	public SpriteToScreenSize biomeMapView;
	public SpriteToScreenSize tempMapView;
	public SpriteToScreenSize precMapView;

	private int[] tempIntensityArray;
	private int[] precIntensityArray;

	private bool biomeGenerationFinished = false;
	//contains a list of region neighbor ids for every region
	private List<int>[] tempRegionNeighbors;
	private List<int>[] precRegionNeighbors;

	//Temp (0-2) & Prec (3-5) Colors
	private Dictionary<int, Color32> tempColors = new Dictionary<int, Color32>() {
		{0, new Color32(255, 216, 40, 255)},
		{1, new Color32(252, 114, 8, 255)},
		{2, new Color32(204, 10, 10, 255)}
	};
	private Dictionary<int, Color32> precColors = new Dictionary<int, Color32>() {
		{0, new Color32(153, 204, 255, 255)},
		{1, new Color32(0, 153, 255, 255)},
		{2, new Color32(0, 80, 255, 255)}
	};
	private Texture2D biomeTexture;
	private Texture2D tempTexture;
	private Texture2D precTexture;
	private Vector2Int textureSize;
	private WizardState currentWizardState;
	private List<Vector2Int> localMinima;

	void Start() {
		generateNewMap = true;
		currentWizardState = WizardState.TerrainView;
		wizardState = WizardState.BiomeView;
		localMinima = new List<Vector2Int>();
	}

	private void Update() {
		if(generateNewMap && terrainGenerator.isMeshGenerated()) {
			textureSize = terrainGenerator.getMeshSize();
			generateNewMap = false;
			localMinima = findLocalMinima(terrainGenerator.getTerrainVertices(), localMinimumArea);
			if(textureSize.x * textureSize.y > 0) {
				int centroidCount;
				if(useMinimaForCentroids) {
					centroidCount = localMinima.Count;
				} else {
					centroidCount = regionAmount;
				}
				
				//init region neighbor arrays
				tempRegionNeighbors = new List<int>[centroidCount];
				for (int i = 0; i < centroidCount; i++) {
					tempRegionNeighbors[i] = new List<int>();
				}
				precRegionNeighbors = new List<int>[centroidCount];
				for (int i = 0; i < centroidCount; i++) {
					precRegionNeighbors[i] = new List<int>();
				}


				//generate/set intensityArrays, textures, sprites
				tempIntensityArray = calculateVoronoiDiagram(true);
				tempIntensityArray = addMountainTemperatures(tempIntensityArray);
				tempTexture = GetTextureFromIntensityArray(tempIntensityArray, tempColors);
				tempSprite.GetComponent<SpriteRenderer>().sprite = buildSprite(tempTexture);

				precIntensityArray = calculateVoronoiDiagram(false);
				precIntensityArray = addWaterHumidity(precIntensityArray);
				
				precTexture = GetTextureFromIntensityArray(precIntensityArray, precColors);
				precSprite.GetComponent<SpriteRenderer>().sprite = buildSprite(precTexture);

				biomeTexture = addSaturationToTexture(MergeTextures(tempTexture, precTexture), 0.5f);
				biomeSprite.GetComponent<SpriteRenderer>().sprite = buildSprite(biomeTexture);
				biomeGenerationFinished = true;

				biomeMapView.resize();
				tempMapView.resize();
				precMapView.resize();
			} else {
				Debug.Log("Texture size too small");
			}
		}

		if(biomeGenerationFinished) {
			if(biome_mergeTempAndPrec) {
				biome_mergeTempAndPrec = false;
				biomeTexture = addSaturationToTexture(MergeTextures(tempTexture, precTexture), 0.5f);
				biomeSprite.GetComponent<SpriteRenderer>().sprite = buildSprite(biomeTexture);
				biomeSprite.GetComponent<FreeDraw.Drawable>().init();
			}

			if(biome_eliminateSmallRegions) {
				biome_eliminateSmallRegions = false;
				biomeTexture = EliminateSmallRegions(biomeTexture, eliminationSizeFactor);
				biomeSprite.GetComponent<SpriteRenderer>().sprite = buildSprite(biomeTexture);
				biomeSprite.GetComponent<FreeDraw.Drawable>().init();
			}

			if(biome_openSprite) {
				biome_openSprite = false;
				List<Color> currentBiomeColors = determineBiomeColors(biomeTexture);
				biomeTexture = openTexture(currentBiomeColors, biomeTexture);
				biomeSprite.GetComponent<SpriteRenderer>().sprite = buildSprite(biomeTexture);
				biomeSprite.GetComponent<FreeDraw.Drawable>().init();
			}

			controlSpriteWizard();
		}
	}

	List<Color> determineBiomeColors(Texture2D biomeTexture) {
		List<Color> biomeColors = new List<Color>();
		for(int y = 0; y < biomeTexture.height; y++) {
			for(int x = 0; x < biomeTexture.width; x++) {
				Color pixelColor = biomeTexture.GetPixel(x,y);
				if(!biomeColors.Contains(pixelColor)) {
					biomeColors.Add(pixelColor);
				}
			}
		}
		return biomeColors;
	}

	Texture2D openTexture(List<Color> imageColors, Texture2D texture) {
		foreach(Color color in imageColors) {
			texture = erodeTexture(color, texture);
			texture = dilateTexture(color, texture);
		}
		return texture;
	}

	Texture2D erodeTexture(Color erosionColor, Texture2D texture) {
		Texture2D resultTexture = new Texture2D(textureSize.x, textureSize.y);
		resultTexture.filterMode = FilterMode.Point;
		for(int y = 0; y < textureSize.y; y++) {
			for(int x = 0; x < textureSize.x; x++) {
				resultTexture.SetPixel(x,y,texture.GetPixel(x,y));
			}
		}
		
		for(int y = 0; y < textureSize.y; y++) {
			for(int x = 0; x < textureSize.x; x++) {
				if(texture.GetPixel(x,y) == erosionColor) {
					if(x-1 >= 0 && texture.GetPixel(x-1,y) != erosionColor) {
						resultTexture.SetPixel(x,y,texture.GetPixel(x-1,y));
					} else if(x+1 < textureSize.x && texture.GetPixel(x+1,y) != erosionColor) {
						resultTexture.SetPixel(x,y,texture.GetPixel(x+1,y));
					} else if(y-1 >= 0 && texture.GetPixel(x,y-1) != erosionColor) {
						resultTexture.SetPixel(x,y,texture.GetPixel(x,y-1));
					} else if(y+1 < textureSize.y && texture.GetPixel(x,y+1) != erosionColor) {
						resultTexture.SetPixel(x,y,texture.GetPixel(x,y+1));
					}
				}
			}
		}
		resultTexture.Apply();
		return resultTexture;
	}

	Texture2D dilateTexture(Color dilatationColor, Texture2D texture) {
		Texture2D resultTexture = new Texture2D(textureSize.x, textureSize.y);
		resultTexture.filterMode = FilterMode.Point;
		for(int y = 0; y < textureSize.y; y++) {
			for(int x = 0; x < textureSize.x; x++) {
				resultTexture.SetPixel(x,y,texture.GetPixel(x,y));
			}
		}

		for(int y = 0; y < textureSize.y; y++) {
			for(int x = 0; x < textureSize.x; x++) {
				if(texture.GetPixel(x,y) != dilatationColor) {
					if(x-1 > 0 && texture.GetPixel(x-1,y) == dilatationColor) {
						resultTexture.SetPixel(x,y,dilatationColor);
					} else if(x+1 < textureSize.x && texture.GetPixel(x+1,y) == dilatationColor) {
						resultTexture.SetPixel(x,y,dilatationColor);
					} else if(y-1 > 0 && texture.GetPixel(x,y-1) == dilatationColor) {
						resultTexture.SetPixel(x,y,dilatationColor);
					} else if(y+1 < textureSize.y && texture.GetPixel(x,y+1) == dilatationColor) {
						resultTexture.SetPixel(x,y,dilatationColor);
					}
				}
			}
		}
		resultTexture.Apply();
		return resultTexture;
	}

	void controlSpriteWizard() {
		if(wizardState != currentWizardState) {
			currentWizardState = wizardState;
			switch (wizardState) {
				case WizardState.TempView:
					terrainCam.SetActive(false);
					spriteCam.SetActive(true);

					tempSprite.SetActive(true);
					precSprite.SetActive(false);
					biomeSprite.SetActive(false);
					tempSprite.GetComponent<FreeDraw.Drawable>().setPenColor(tempColors[0]);
					break;
				case WizardState.PrecView:
					terrainCam.SetActive(false);
					spriteCam.SetActive(true);

					tempSprite.SetActive(false);
					precSprite.SetActive(true);
					biomeSprite.SetActive(false);
					precSprite.GetComponent<FreeDraw.Drawable>().setPenColor(precColors[0]);
					break;
				case WizardState.BiomeView:
					terrainCam.SetActive(false);
					spriteCam.SetActive(true);

					tempSprite.SetActive(false);
					precSprite.SetActive(false);
					biomeSprite.SetActive(true);
					biomeSprite.GetComponent<FreeDraw.Drawable>().setPenColor(biomeTexture.GetPixel(0,0));
					break;
				case WizardState.TerrainView:
					spriteCam.SetActive(false);
					terrainCam.SetActive(true);

					tempSprite.SetActive(false);
					precSprite.SetActive(false);
					biomeSprite.SetActive(false);
					break;
			}
			biomeMapView.resize();
			tempMapView.resize();
			precMapView.resize();
		}
	}

	int[] addMountainTemperatures(int[] intensityArray) {
		Vector3[] terrainVertices = terrainGenerator.getTerrainVertices();
		float maxHeight = terrainGenerator.getMaxTerrainheight();
		float minHeight = terrainGenerator.getMinTerrainheight();
		Vector2Int meshSize = terrainGenerator.getMeshSize();

		float coldHeightsLimit = maxHeight - ((maxHeight - minHeight) * realitveColdHeightsLimit);

		for(int meshY = 0; meshY < meshSize.y; meshY++) {
			for(int meshX = 0; meshX < meshSize.x; meshX++) {
				int meshIndex = meshX + meshY * meshSize.x;
				if(meshX < textureSize.x && meshY < textureSize.y && terrainVertices[meshIndex].y > coldHeightsLimit) {
					int textureX = meshIndex % meshSize.x;
					int textureY = Mathf.FloorToInt(meshIndex / meshSize.x);
					int textureIndex = textureX + textureY * textureSize.x;
					intensityArray[textureIndex] = Mathf.Max(intensityArray[textureIndex] - 1, 0);
				}
			}
		}
		return intensityArray;
	}

	int[] addWaterHumidity(int[] intensityArray) {
		Vector3[] terrainVertices = terrainGenerator.getTerrainVertices();
		float maxHeight = terrainGenerator.getMaxTerrainheight();
		float minHeight = terrainGenerator.getMinTerrainheight();
		Vector2Int meshSize = terrainGenerator.getMeshSize();
		float relativeWaterHeight = terrainGenerator.getRelativeWaterHeight();

		float waterLevel = minHeight + ((maxHeight - minHeight) * relativeWaterHeight);

		//determine all mesh-nodes under water
		bool[] isWaterNode = new bool[terrainVertices.Length];
		for(int meshY = 0; meshY < meshSize.y; meshY++) {
			for(int meshX = 0; meshX < meshSize.x; meshX++) {
				int meshIndex = meshX + meshY * meshSize.x;
				if(meshX < textureSize.x && meshY < textureSize.y && terrainVertices[meshIndex].y < waterLevel) {
					isWaterNode[meshIndex] = true;
				} else {
					isWaterNode[meshIndex] = false;
				}
			}
		}

		//determine mesh-nodes in area around water
		//dilate water formations humiditySpread-Times
		for(int i = 0; i < humiditySpread; i++) {
			isWaterNode = dilateWaterNodes(isWaterNode);
		}

		//add intensity values
		for(int meshY = 0; meshY < meshSize.y; meshY++) {
			for(int meshX = 0; meshX < meshSize.x; meshX++) {
				int meshIndex = meshX + meshY * meshSize.x;
				if(isWaterNode[meshIndex]) {
					int textureX = meshIndex % meshSize.x;
					int textureY = Mathf.FloorToInt(meshIndex / meshSize.x);
					int textureIndex = textureX + textureY * textureSize.x;
					intensityArray[textureIndex] = Mathf.Min(intensityArray[textureIndex] + 1, 2);
				}
			}
		}
		return intensityArray;

		bool[] dilateWaterNodes(bool[] waterNodes) {
			bool[] resultArray = (bool[])waterNodes.Clone();

			for(int meshY = 0; meshY < meshSize.y; meshY++) {
				for(int meshX = 0; meshX < meshSize.x; meshX++) {
					int meshIndex = meshX + meshY * meshSize.x;
					int meshIndexUp = meshX + (meshY+1) * meshSize.x;
					int meshIndexRight = (meshX+1) + meshY * meshSize.x;
					int meshIndexDown = meshX + (meshY-1) * meshSize.x;
					int meshIndexLeft = (meshX-1) + meshY * meshSize.x;
					if(!waterNodes[meshIndex]) {
						if(meshX-1 > 1 && waterNodes[meshIndexLeft]) {
							resultArray[meshIndex] = true;
						} else if(meshX+1 < textureSize.x-1 && waterNodes[meshIndexRight]) {
							resultArray[meshIndex] = true;
						} else if(meshY-1 > 1 && waterNodes[meshIndexDown]) {
							resultArray[meshIndex] = true;
						} else if(meshY+1 < textureSize.y-1 && waterNodes[meshIndexUp]) {
							resultArray[meshIndex] = true;
						}
					}
				}
			}
			return resultArray;
		}
	}

	private Texture2D EliminateSmallRegions(Texture2D texture, float eliminationSizeFactor) {
		Dictionary<Vector2Int, int> pixelRegions = new Dictionary<Vector2Int, int>();
		
		//breath first search -> save sizes of all regions
		int regionCount = 0;
		Dictionary<int, int> regionSizes = new Dictionary<int, int>(); //regionID, regionSize
		for(int x = 0; x < textureSize.x; x++) {
			for(int y = 0; y < textureSize.y; y++) {
				Vector2Int startingPixel = new Vector2Int(x,y);
				if(!pixelRegions.ContainsKey(startingPixel)) {
					regionSizes.Add(regionCount, 0);
					List<Vector2Int> pixelsToCheck = new List<Vector2Int>();
					pixelsToCheck.Add(startingPixel);

					while(pixelsToCheck.Count > 0) {
						Vector2Int currentPixel = pixelsToCheck[0];
						pixelsToCheck.RemoveAt(0);
						if(isPixelInTexture(currentPixel) && isPixelColorEqual(startingPixel, currentPixel) && !pixelRegions.ContainsKey(currentPixel)) {
							pixelRegions.Add(currentPixel, regionCount);
							regionSizes[regionCount]++;
							pixelsToCheck.Add(new Vector2Int(currentPixel.x+1, currentPixel.y));
							pixelsToCheck.Add(new Vector2Int(currentPixel.x-1, currentPixel.y));
							pixelsToCheck.Add(new Vector2Int(currentPixel.x, currentPixel.y+1));
							pixelsToCheck.Add(new Vector2Int(currentPixel.x, currentPixel.y-1));
						}
					}
					regionCount++;
				}
			}
		}

		//find biggest and second biggest region of all regions
		Vector2Int biggestRegion = new Vector2Int(-1,0); //regionID, regionSize
		Vector2Int secondBiggestRegion = new Vector2Int(-1,0); //regionID, regionSize
		foreach(KeyValuePair<int, int> regionSize in regionSizes) {
			if(regionSize.Value > biggestRegion.y) {
				secondBiggestRegion = biggestRegion;
				biggestRegion.x = regionSize.Key;
				biggestRegion.y = regionSize.Value;
			} else if(regionSize.Value > secondBiggestRegion.y) {
				secondBiggestRegion.x = regionSize.Key;
				secondBiggestRegion.y = regionSize.Value;
			}
		}

		//detemine minimum size for regions to not get eliminated
		//always let at least the biggest 2 regions exist
		int secondBiggestRegionSize = secondBiggestRegion.y;
		int minRegionSize = Mathf.Min(Mathf.RoundToInt(textureSize.x * textureSize.y * eliminationSizeFactor),secondBiggestRegionSize);

		//list which regions should be eliminated by ID
		List<int> regionsToEliminate = new List<int>();
		for(int i = 0; i < regionSizes.Count; i++) {
			if(regionSizes[i] < minRegionSize) {
				regionsToEliminate.Add(i);
			}
		}

		//eliminate chosen regions by recoloring them
		Color[] resultColorArray = new Color [textureSize.x * textureSize.y];
		for(int y = 0; y < textureSize.y; y++) {
			for(int x = 0; x < textureSize.x; x++) {
				Vector2Int pixel = new Vector2Int(x,y);
				int pixelRegion = pixelRegions[pixel];
				if(regionsToEliminate.Contains(pixelRegion)) {
					resultColorArray[y*textureSize.x + x] = determineNewPixelColor(pixel);
				} else {
					resultColorArray[y*textureSize.x + x] = texture.GetPixel(pixel.x, pixel.y);
				}
			}
		}

		//end of function
		return GetTextureFromColorArray(resultColorArray);


		//some helper functions:

		//get nearest pixelcolor in neighborhood that should not be removed
		Color determineNewPixelColor(Vector2Int pixel) {
			int maxDistanceFromPixel = Mathf.Max(textureSize.x, textureSize.y);
			Vector2Int upperPixel = new Vector2Int(pixel.x, pixel.y+1);
			Vector2Int rightPixel = new Vector2Int(pixel.x+1, pixel.y);
			Vector2Int lowerPixel = new Vector2Int(pixel.x, pixel.y-1);
			Vector2Int leftPixel = new Vector2Int(pixel.x-1, pixel.y);

			//check closest (distance 1) in 4er neighborhood
			if(hasPossibleColor(upperPixel))
				return texture.GetPixel(upperPixel.x, upperPixel.y);
			if(hasPossibleColor(rightPixel))
				return texture.GetPixel(rightPixel.x, rightPixel.y);
			if(hasPossibleColor(lowerPixel))
				return texture.GetPixel(lowerPixel.x, lowerPixel.y);
			if(hasPossibleColor(leftPixel))
				return texture.GetPixel(leftPixel.x, leftPixel.y);

			//check rest in growing circles around pixel
			for(int distance = 2; distance < maxDistanceFromPixel; distance++) {
				for(int xOffset = -distance; xOffset <= distance; xOffset++) {
					Vector2Int pixelToCheck = new Vector2Int(pixel.x + xOffset, pixel.y + distance);
					if(hasPossibleColor(pixelToCheck))
						return texture.GetPixel(pixelToCheck.x, pixelToCheck.y);
				}
				for(int yOffset = 1-distance; yOffset <= -1+distance; yOffset++) {
					Vector2Int pixelToCheck = new Vector2Int(pixel.x + distance, pixel.y + yOffset);
					if(hasPossibleColor(pixelToCheck))
						return texture.GetPixel(pixelToCheck.x, pixelToCheck.y);
				}
				for(int xOffset = -distance; xOffset <= distance; xOffset++) {
					Vector2Int pixelToCheck = new Vector2Int(pixel.x + xOffset, pixel.y - distance);
					if(hasPossibleColor(pixelToCheck))
						return texture.GetPixel(pixelToCheck.x, pixelToCheck.y);
				}
				for(int yOffset = 1-distance; yOffset <= -1+distance; yOffset++) {
					Vector2Int pixelToCheck = new Vector2Int(pixel.x - distance, pixel.y + yOffset);
					if(hasPossibleColor(pixelToCheck))
						return texture.GetPixel(pixelToCheck.x, pixelToCheck.y);
				}
			}
			return Color.white;

			bool hasPossibleColor(Vector2Int pixelToCheck) {
				return isPixelInTexture(pixelToCheck) && !regionsToEliminate.Contains(pixelRegions[pixelToCheck]);
			}
		}

		bool isPixelColorEqual(Vector2Int pixel1, Vector2Int pixel2) {
			return texture.GetPixel(pixel1.x, pixel1.y) == texture.GetPixel(pixel2.x, pixel2.y);
		}
	}

	bool isPixelInTexture(Vector2Int pixel) {
		return (pixel.x >= 0 && pixel.x < textureSize.x && pixel.y >= 0 && pixel.y < textureSize.y);
	}

	bool isPixelInTerrain(Vector2Int pixel) {
		return (pixel.x >= 1 && pixel.x < textureSize.x-1 && pixel.y >= 1 && pixel.y < textureSize.y-1);
	}

	//areaSize: radius of area around a pixel to check for local minimum
	List<Vector2Int> findLocalMinima(Vector3[] meshVertices, int searchArea) {

		//check for every pixel if it is a local minimum for the chosen area around it
		List<Vector2Int> minimaList = new List<Vector2Int>();
		for(int meshY = 1; meshY < textureSize.y-1; meshY++) {
			for(int meshX = 1; meshX < textureSize.x-1; meshX++) {
				if(isLocalMinimum(meshX, meshY)) {
					minimaList.Add(new Vector2Int(meshX, meshY));
				}
			}
		}
		return minimaList;

		//check if there are vertices with lower high in the chosen area around
		bool isLocalMinimum(int x, int y) {
			bool isLocalMinimum = true;
			for(int offsetX = -searchArea; offsetX <= searchArea; offsetX++) {
				for(int offsetY = -searchArea; offsetY <= searchArea; offsetY++) {
					isLocalMinimum = isLocalMinimum && !isOtherPixelLower(x + offsetX, y + offsetY, x, y);
					if(!isLocalMinimum) {
						return false;
					}
				}
			}
			return isLocalMinimum;
		}

		//true if: pixel is in texture & pixelHeight is smaller than currentPixelHeight
		bool isOtherPixelLower(int x, int y, int currX, int currY) {
			return isPixelInTerrain(new Vector2Int(x, y)) && meshVertices[meshIndex(x, y)].y < meshVertices[meshIndex(currX, currY)].y;
		}

		int meshIndex(int x, int y) {
			return x + y * textureSize.x;
		}
	}

	//calc voronoi diagram: array of intensity values (0-2)
	private int[] calculateVoronoiDiagram(bool isTempDiagram) {
		Vector2Int[] centroids;
		if(useMinimaForCentroids) {
			centroids = new Vector2Int[localMinima.Count];;
			for(int i = 0; i < localMinima.Count; i++)
			{
				centroids[i] = localMinima[i];
			}
		} else {
			//roll centroids
			centroids = new Vector2Int[regionAmount];
			for(int i = 0; i < regionAmount; i++)
			{
				centroids[i] = new Vector2Int(Random.Range(0, textureSize.x), Random.Range(0, textureSize.y));
			}
		}
		
		//determine region id of each pixel
		int[] pixelRegions = new int[textureSize.x * textureSize.y];
        int index = 0;
		for(int y = 0; y < textureSize.y; y++)
		{
			for(int x = 0; x < textureSize.x; x++)
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
		
		//determine intensity id for every pixel
		int[] pixelIntensityIDs = new int [textureSize.x * textureSize.y];
		for (int i = 0; i < pixelIntensityIDs.Length; i++) {
			int pixelIntensityID = regionIntensityIDs[pixelRegions[i]];
			pixelIntensityIDs[i] = pixelIntensityID;
		}
		return pixelIntensityIDs;

		int[] CalcRegionIntensityIDs(bool calcTempDiagram, List<int>[] regionNeighbors) {
			int[] regionIntensityIDs = new int[centroids.Length];
			for (int i = 0; i < centroids.Length; i++) {
				regionIntensityIDs[i] = -1;
			}
			for (int i = 0; i < centroids.Length; i++) {
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
	}

	private int rollIntensityID(bool calcTempDiagram, int lowestIntensityID, int highestIntensityID) {
		float weightFactor;
		if(calcTempDiagram) {
			weightFactor = averageTemp;
		} else {
			weightFactor = averagePrec;
		}
		int intensityID;
		float dieValue = Random.Range(0f, 1f);
		//there are 3 possible color IDs
		if((highestIntensityID - lowestIntensityID) == 3) {
			if(dieValue < 1-weightFactor) {
				intensityID = 0;
			} else {
				intensityID = 2;
			}
		} else {
			//there are 2 possible color IDs
			if(lowestIntensityID == 0) {
				//possible color IDs are 0 and 1
				if(dieValue < 1-weightFactor)
					intensityID = 0;
				else
					intensityID = 1;
			} else {
				//possible color IDs are 1 and 2
				if(dieValue < 1-weightFactor)
					intensityID = 1;
				else
					intensityID = 2;
			}
		}
		return intensityID;
	}
	int GetClosestCentroidIndex(Vector2Int pixelPos, Vector2Int[] centroids, bool calcTempDiagram) {
		float smallestDst = float.MaxValue;
		int nearestRegionID = 0;
		int secondNearestRegionID = 0;

		//find nearest region id
		for(int i = 0; i < centroids.Length; i++) {
			float distance = calcMinkowskiDistance(pixelPos, centroids[i], minkowskiLambda);
			if (distance < smallestDst) {
				nearestRegionID = i;
				smallestDst = distance;
			}
		}

		//find second nearest region id (determine neighboring regions)
		smallestDst = float.MaxValue;
		for(int i = 0; i < centroids.Length; i++)
		{
			if (i != nearestRegionID) {
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
		Color[] pixelColors = new Color[textureSize.x * textureSize.y];

		int index = 0;
		for (int y = 0; y < textureSize.y; y++) {
			for (int x = 0; x < textureSize.x; x++) {
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

		if(showLocalMinima) {
			foreach(Vector2Int minimum in localMinima) {
				int index = minimum.x + minimum.y * terrainGenerator.getMeshSize().x;
				pixelColors[index] = Color.black;
			}
		}

		return GetTextureFromColorArray(pixelColors);
	}

	private Texture2D GetTextureFromColorArray(Color[] pixelColors) {
		Texture2D tex = new Texture2D(textureSize.x, textureSize.y);
		tex.filterMode = FilterMode.Point;
		tex.SetPixels(pixelColors);
		tex.Apply();
		return tex;
	}

	public Texture2D getBiomeTexture() {
		return biomeTexture;
	}

	public Texture2D getTempTexture() {
		return tempSprite.GetComponent<SpriteRenderer>().sprite.texture;
	}

	public Texture2D getPrecTexture() {
		return precSprite.GetComponent<SpriteRenderer>().sprite.texture;
	}

	public bool isBiomeTextureGenerated() {
		return biomeGenerationFinished;
	}

	public Color mergeColors(Color color1, Color color2, float mergeProportions) {
		return Color.Lerp(color1, color2, Mathf.Clamp(mergeProportions, 0f, 1f));
	}

	public Color addSaturation(Color oldColor, float satAddition) {
		Vector3 hsvColor = new Vector3(0,0,0);
		Color.RGBToHSV(oldColor, out hsvColor.x, out hsvColor.y, out hsvColor.z);
		return Color.HSVToRGB(hsvColor.x, Mathf.Min(hsvColor.y+satAddition, 1f), hsvColor.z);
	}

	public Texture2D addSaturationToTexture(Texture2D texture, float satAddition) {
		for(int y = 0; y < textureSize.y; y++) {
			for(int x = 0; x < textureSize.x; x++) {
				texture.SetPixel(x,y,addSaturation(texture.GetPixel(x,y),0.5f));
			}
		}
		texture.Apply();
		return texture;
	}

	private Sprite buildSprite(Texture2D texture) {
		return Sprite.Create(texture, new Rect(0, 0, textureSize.x, textureSize.y), Vector2.one * 0.5f);
	}
}