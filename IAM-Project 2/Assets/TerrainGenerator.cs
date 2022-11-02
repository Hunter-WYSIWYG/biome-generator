using UnityEngine;
using System;

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
        SetPredefinedParameters();
        UpdateMeshes();
    }

    void UpdateMeshes()
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
        Vector3[] terrainVertices = new Vector3[4*(meshLength + 1)*(meshWidth + 1)];
        Vector2[] terrainUVs = new Vector2[4*(meshLength + 1)*(meshWidth + 1)];
        int[] terrainTriangles = new int [meshLength * meshWidth * 6];
        Color[] terrainColors = new Color[terrainVertices.Length];

        terrainVertices = CalcTerrainVertices(terrainVertices);
        terrainUVs = CalcTerrainUVs(terrainUVs);
        terrainTriangles = CalcTerrainTriangles(terrainTriangles);
        terrainColors = CalcTerrainColors(terrainColors, terrainVertices);

        terrainMesh.Clear();
        terrainMesh.vertices = terrainVertices;
        terrainMesh.triangles = terrainTriangles;
        terrainMesh.uv = terrainUVs;
        //terrainMesh.colors = terrainColors;
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();
    }

    Vector2[] CalcTerrainUVs(Vector2[] terrainUVs) {

        for (int index = 0, z = 0; z < meshWidth; z++) {
            for (int x = 0; x < meshLength; x++) {
                terrainUVs[index * 4 + 0] = new Vector2(0, 0);
                terrainUVs[index * 4 + 1] = new Vector2(0, 1);
                terrainUVs[index * 4 + 2] = new Vector2(1, 1);
                terrainUVs[index * 4 + 3] = new Vector2(1, 0);
                index++;
            }
        }
        
        return terrainUVs;
    }

    Color[] CalcTerrainColors(Color[] terrainColors, Vector3[] terrainVertices) {
        if (lockGradientOnCurrentHeights) {
            gradientMinValue = minTerrainHeight;
            gradientMaxValue = maxTerrainHeight;
            lockGradientOnCurrentHeights = false;
        }

        for (int i = 0, z = 0; z < meshWidth; z++)
        {
            for (int x = 0; x < meshLength; x++)
            {
                float gradientValue = Mathf.InverseLerp(gradientMinValue, gradientMaxValue, terrainVertices[i].y);
                terrainColors[i] = usedGradient.Evaluate(gradientValue);
                i++;
            }
        }
        return terrainColors;
    }

    int[] CalcTerrainTriangles(int[] terrainTriangles) {
        int currentVertex = 0;
        int currentTriangles = 0;
        for (int z = 0; z < meshWidth; z++)
        {
            for (int x = 0; x < meshLength; x++)
            {
                terrainTriangles[0 + currentTriangles] = 4 * currentVertex + 0;
                terrainTriangles[1 + currentTriangles] = 4 * currentVertex + 1;
                terrainTriangles[2 + currentTriangles] = 4 * currentVertex + 2;
                terrainTriangles[3 + currentTriangles] = 4 * currentVertex + 0;
                terrainTriangles[4 + currentTriangles] = 4 * currentVertex + 2;
                terrainTriangles[5 + currentTriangles] = 4 * currentVertex + 3;

                currentVertex++;
                currentTriangles += 6;
            }
        }
        return terrainTriangles;
    }

    Vector3[] CalcTerrainVertices(Vector3[] terrainVertices)
    {
        for (int nodeIndex = 0, z = 0; z < meshWidth; z++)
        {
            for (int x = 0; x < meshLength; x++)
            {
                terrainVertices[nodeIndex * 4 + 0] = new Vector3(x, CalcVertexHeight(x, z), z);
                terrainVertices[nodeIndex * 4 + 1] = new Vector3(x, CalcVertexHeight(x, z+1), z + 1);
                terrainVertices[nodeIndex * 4 + 2] = new Vector3(x + 1, CalcVertexHeight(x+1, z+1), z + 1);
                terrainVertices[nodeIndex * 4 + 3] = new Vector3(x + 1, CalcVertexHeight(x+1, z), z);
                nodeIndex++;

                float CalcVertexHeight(int x, int z) {
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
                        vertexHeight = AddNoiseLayers(vertexHeight, lowerNoiseLayerHeightVariety, lowerNoiseLayerDensity, x, z);
                    } else {
                        vertexHeight = AddNoiseLayers(vertexHeight, upperNoiseLayerHeightVariety, upperNoiseLayerDensity, x, z);
                    }

                    //noise layers can rarely lower the terrain into negative domain
                    //clamp that to 0 to prevent imaginary numbers in the powering step
                    vertexHeight = Math.Max(0,vertexHeight);
                    
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

                    return vertexHeight;
                }

                float AddNoiseLayers(float height, float noiseLayerHeightVariety, float noiseLayerDensity, int x, int z)
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
        for (int i = 0; i < terrainVertices.Length; i++)
        {
            float xCoord = terrainVertices[i].x;
            float zCoord = terrainVertices[i].z;
            if (xCoord == 0) {
                terrainVertices[i] = new Vector3(xCoord+1, minTerrainHeight, zCoord);
            }
            if (xCoord == meshLength) {
                terrainVertices[i] = new Vector3(xCoord-1, minTerrainHeight, zCoord);
            }
            if (zCoord == 0) {
                terrainVertices[i] = new Vector3(Mathf.Clamp(xCoord, 1, meshLength-1), minTerrainHeight, zCoord+1);
            }
            if (zCoord == meshWidth) {
                terrainVertices[i] = new Vector3(Mathf.Clamp(xCoord, 1, meshLength-1), minTerrainHeight, zCoord-1);
            }
        }

        return terrainVertices;
    }

    void SetPredefinedParameters() {
        if (generateMountains) {
            currentLandscapeID = mountainsID;
            setFalseExcept(mountainsID);
            SetMountainsSettings();
            generateMountains = false;
        }
        if (generateIslands) {
            currentLandscapeID = islandsID;
            setFalseExcept(islandsID);
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

        void setFalseExcept(int landscapeID) {
            switch (landscapeID) {
                case 1:
                    generateMountains = true;
                    generateIslands = false;
                    break;
                case 2:
                    generateMountains = false;
                    generateIslands = true;
                    break;
                default:
                    generateMountains = false;
                    generateIslands = false;
                    break;
            }
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
