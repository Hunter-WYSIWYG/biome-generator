using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class TerrainGenerator : MonoBehaviour
{
    [Header("Base Settings")]
    public int terrainLenght = 100;
    public int terrainWidth = 100;
    public bool generateMountains = false;
    public bool generateIslands = false;

    [Header("Movement Settings")]
    public float noiseOffsetX = 0.0f;
    public float noiseOffsetZ = 0.0f;
    public bool moveTerrainX = false;
    public bool moveTerrainZ = false;
    public float terrainMoveSpeed = 0.4f;

    [Header("Height Settings")]
    //terrain height differences
    public float heightVariety = 10f;

    //terrain steepness
    public float noisePower = 1f;

    //frequency of mountains and valleys
    [Range(0,0.5f)]
    public float heightDensity = 0.05f;

    //height percentage of the generated terrain in which the water plane is set
    [Range(0,1)]
    public float relativeWaterHeight = 0f;

    //lock to prevent plane height from changing when more, randomly higher or lower terrain is generated (plane height is relative!)
    //click once u have edited the height settings
    public bool lockWaterPlaneOnCurrentHeights = false;

    //lock to prevent gradient from changing when more, randomly higher or lower terrain is generated (gradient heights are relative!)
    //click once u have edited the height settings
    public bool lockGradientOnCurrentHeights = false;


    [Header("Noise Layer Settings")]
    //terrain roughness height
    [Range(0,1)]
    public float lowerNoiseLayerHeightVariety = 0.2f;

    //terrain roughness density
    [Range(0,1)]
    public float lowerNoiseLayerDensity = 0.2f;

    //define border where upper noise layer starts beeing applied; [0,1]
    //set 0 to disable
    [Range(0,1)]
    public float upperHeightPercent = 0f;

    //terrain roughness height for upper height area
    [Range(0,1)]
    public float upperNoiseLayerHeightVariety = 0.2f;

    //terrain roughness density for upper height area
    [Range(0,1)]
    public float upperNoiseLayerDensity = 0.2f;

    [Header("Color Settings")]
    public Gradient usedGradient;
    public Color waterPlaneColor;

    [Header("Predefined Gradients")]
    public Gradient mountainGradient;
    public Gradient islandGradient;

    [Header("Biomes")]
    public bool showGradientColors = false;
    public bool showBiomeColors = false;
    public bool showTemperatureColors = false;
    public bool showPrecipitationColors = false;
    //make temperatures lower at mountains tops
    public BiomeGenerator biomeGenerator;
    
    private int currentColorMapID = 0;
    private static int gradientMapID = 0;
    private static int biomeMapID = 1;
    private static int temperatureMapID = 2;
    private static int precipitationMapID = 3;
    private int currentLandscapeID = 0;
    private static int mountainsID = 1;
    private static int islandsID = 2;
    private float minTerrainHeight = 0f;
    private float maxTerrainHeight = 0f;
    private int meshLength = 0;
    private int meshWidth = 0;
    private float gradientMinValue = 0f;
    private float gradientMaxValue = 0f;


    Mesh terrainMesh;

    void Start()
    {
        terrainMesh = new Mesh();
        terrainMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        GetComponent<MeshFilter>().mesh = terrainMesh;
        lockGradientOnCurrentHeights = true;
    }

    private void Update()
    {
        UpdateColorMap();
        SetPredefinedParameters();
        UpdateMesh();
    }

    void UpdateMesh()
    {
        //add necessary nodes to close terrain edges
        meshLength = terrainLenght + 2;
        meshWidth = terrainWidth + 2;

        //update terrain mesh
        CreateTerrainMesh();
        MoveTerrainMesh();
    }

    void MoveTerrainMesh() {
        if (moveTerrainX) {
            noiseOffsetX += Time.deltaTime * terrainMoveSpeed;
        }
        if (moveTerrainZ) {
            noiseOffsetZ += Time.deltaTime * terrainMoveSpeed;
        }
    }

    void CreateTerrainMesh()
    {
        Vector3[] terrainVertices = new Vector3[(meshLength + 1)*(meshWidth + 1)];
        terrainVertices = CalcVertexHeights(terrainVertices);

        int[] terrainTriangles = new int [meshLength * meshWidth * 6];
        int currentVertex = 0;
        int currentTriangles = 0;
        for (int z = 0; z < meshWidth; z++)
        {
            for (int x = 0; x < meshLength; x++)
            {
                terrainTriangles[0 + currentTriangles] = currentVertex;
                terrainTriangles[1 + currentTriangles] = currentVertex + meshLength + 1;
                terrainTriangles[2 + currentTriangles] = currentVertex + 1;
                terrainTriangles[3 + currentTriangles] = currentVertex + 1;
                terrainTriangles[4 + currentTriangles] = currentVertex + meshLength + 1;
                terrainTriangles[5 + currentTriangles] = currentVertex + meshLength + 2;

                currentVertex++;
                currentTriangles += 6;
            }
            currentVertex++;
        }

        Color[] terrainColors = new Color[terrainVertices.Length];

        if (lockGradientOnCurrentHeights) {
            gradientMinValue = minTerrainHeight;
            gradientMaxValue = maxTerrainHeight;
            lockGradientOnCurrentHeights = false;
        }

        bool noTextureAvailable = !biomeGenerator.isBiomeTextureGenerated();
        int index = 0;
        for (int z = 0; z <= meshWidth; z++)
        {
            for (int x = 0; x <= meshLength; x++)
            {
                if(currentColorMapID == gradientMapID || noTextureAvailable) {
                    float gradientValue = Mathf.InverseLerp(gradientMinValue, gradientMaxValue, terrainVertices[index].y);
                    terrainColors[index] = usedGradient.Evaluate(gradientValue);
                    index++;
                } else if(currentColorMapID == biomeMapID) {
                    terrainColors[index] = biomeGenerator.getBiomeTexture().GetPixel(x,z);
                    index++;
                } else if(currentColorMapID == temperatureMapID) {
                    terrainColors[index] = biomeGenerator.getTempTexture().GetPixel(x,z);
                    index++;
                } else if(currentColorMapID == precipitationMapID) {
                    terrainColors[index] = biomeGenerator.getPrecTexture().GetPixel(x,z);
                    index++;
                }
            }
        }

        terrainMesh.Clear();
        terrainMesh.vertices = terrainVertices;
        terrainMesh.triangles = terrainTriangles;
        terrainMesh.colors = terrainColors;
        terrainMesh.RecalculateNormals();
    }



    Vector3[] CalcVertexHeights(Vector3[] meshNodes)
    {
        for (int i = 0, z = 0; z <= meshWidth; z++)
        {
            for (int x = 0; x <= meshLength; x++)
            {
                //base noise values for height calculations
                float noiseXCoord = x * heightDensity + noiseOffsetX;
                float noiseYCoord = z * heightDensity + noiseOffsetZ;
                float vertexHeight = Mathf.PerlinNoise(noiseXCoord, noiseYCoord);

                //calculate breakpoint where upper height area begins (upper X percent of terrain)
                float heightDifference = maxTerrainHeight - minTerrainHeight;
                float upperHeightRange = heightDifference * upperHeightPercent;
                float upperHeightBegin = minTerrainHeight + (heightDifference - upperHeightRange);

                //terrain roughness for upper and lower height areas
                if (Mathf.Lerp(minTerrainHeight, maxTerrainHeight, vertexHeight) <= upperHeightBegin) {
                    vertexHeight = AddNoiseLayers(vertexHeight, lowerNoiseLayerHeightVariety, lowerNoiseLayerDensity);
                } else {
                    vertexHeight = AddNoiseLayers(vertexHeight, upperNoiseLayerHeightVariety, upperNoiseLayerDensity);
                }

                //noise layers can rarely lower the terrain into negative domain
                //clamp that to 0 to prevent imaginary numbers in the powering step
                vertexHeight = Mathf.Max(0,vertexHeight);
                
                //terrain height additions/multiplications/powering
                vertexHeight = Mathf.Pow(vertexHeight, noisePower);
                vertexHeight *= heightVariety;

                //track min/max terrain height
                if (z==0 && x==0) {
                    minTerrainHeight = vertexHeight;
                    maxTerrainHeight = vertexHeight;
                } else {
                    if (vertexHeight < minTerrainHeight)
                        minTerrainHeight = vertexHeight;
                    if (vertexHeight > maxTerrainHeight)
                        maxTerrainHeight = vertexHeight;
                }

                meshNodes[i] = new Vector3(x, vertexHeight, z);

                i++;

                float AddNoiseLayers(float height, float noiseLayerHeightVariety, float noiseLayerDensity)
                {
                    height += noiseLayerHeightVariety *
                        Mathf.PerlinNoise(
                            x * noiseLayerDensity + noiseOffsetX + 5.3f,
                            z * noiseLayerDensity + noiseOffsetZ + 9.1f
                        );
                    height += noiseLayerHeightVariety / 2 *
                        Mathf.PerlinNoise(
                            x * noiseLayerDensity*2 + noiseOffsetX + 17.8f,
                            z * noiseLayerDensity*2 + noiseOffsetZ + 23.5f
                        );
                    height /= (1 + noiseLayerHeightVariety + noiseLayerHeightVariety/2);
                    return height;
                }
            }
        }

        //close the edges of the terrain
        for (int i = 0; i < meshNodes.Length; i++)
        {
            float xCoord = meshNodes[i].x;
            float zCoord = meshNodes[i].z;
            if (xCoord == 0) {
                meshNodes[i] = new Vector3(xCoord+1, minTerrainHeight, zCoord);
            }
            if (xCoord == meshLength) {
                meshNodes[i] = new Vector3(xCoord-1, minTerrainHeight, zCoord);
            }
            if (zCoord == 0) {
                meshNodes[i] = new Vector3(Mathf.Clamp(xCoord, 0, meshLength-1), minTerrainHeight, zCoord+1);
            }
            if (zCoord == meshWidth) {
                meshNodes[i] = new Vector3(Mathf.Clamp(xCoord, 1, meshLength), minTerrainHeight, zCoord-1);
            }
        }

        return meshNodes;
    }

    void SetPredefinedParameters() {
        if (generateMountains) {
            currentLandscapeID = mountainsID;
            SetMountainsSettings();
            generateMountains = false;
        }
        if (generateIslands) {
            currentLandscapeID = islandsID;
            SetIslandsSettings();
            generateIslands = false;
        }

        void SetMountainsSettings() {
            heightDensity = 0.045f;
            heightVariety = 25f;
            noisePower = 2f;
            lowerNoiseLayerHeightVariety = 0.15f;
            lowerNoiseLayerDensity = 0.1f;
            upperHeightPercent = 0.5f;
            upperNoiseLayerHeightVariety = 0.2f;
            upperNoiseLayerDensity = 0.2f;
            relativeWaterHeight = 0.09f;
            usedGradient = mountainGradient;
            lockWaterPlaneOnCurrentHeights = true;
            lockGradientOnCurrentHeights = true;
        }

        void SetIslandsSettings() {
            heightDensity = 0.035f;
            heightVariety = 10;
            noisePower = 1.5f;
            lowerNoiseLayerHeightVariety = 0.2f;
            lowerNoiseLayerDensity = 0.15f;
            upperHeightPercent = 0.341f;
            upperNoiseLayerHeightVariety = 0.2f;
            upperNoiseLayerDensity = 0.3f;
            relativeWaterHeight = 0.434f;
            usedGradient = islandGradient;
            lockWaterPlaneOnCurrentHeights = true;
            lockGradientOnCurrentHeights = true;
        }
    }

    void UpdateColorMap() {
        if(showGradientColors) {
            showGradientColors = false;
            currentColorMapID = gradientMapID;
        }
        if(showBiomeColors) {
            showBiomeColors = false;
            currentColorMapID = biomeMapID;
        }
        if(showTemperatureColors) {
            showTemperatureColors = false;
            currentColorMapID = temperatureMapID;
        }
        if(showPrecipitationColors) {
            showPrecipitationColors = false;
            currentColorMapID = precipitationMapID;
        }
    }

    public float getMinTerrainheight()
    {
        return minTerrainHeight;
    }

    public float getMaxTerrainheight()
    {
        return maxTerrainHeight;
    }
}
