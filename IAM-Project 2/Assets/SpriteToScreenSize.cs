using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteToScreenSize : MonoBehaviour
{
    public BiomeGenerator biomeGenerator;
    private bool resizeToScreenSize = false;
    private float spriteWidth;
    private float spriteHeight;
    private int screenWidth_px = Screen.width;
    private int screenHeight_px = Screen.height;
    private float screenWidth;
    private float screenHeight;
    private Vector3 topRightScreenCorner;
    private float scalingFactor = 1;
    private BoxCollider2D boxCollider;
    private bool spriteResized = false;

    void Start() {
        resizeToScreenSize = true;
    }

    void Update()
    {
        if(biomeGenerator.isBiomeTextureGenerated()) {
            if (screenWidth_px != Screen.width || screenHeight_px != Screen.height || resizeToScreenSize) {
                screenWidth_px = Screen.width;
                screenHeight_px = Screen.height;
                resizeToScreenSize = false;
                resize();
                spriteResized = true;
            }
        }

        if(biomeGenerator.generateNewMap) {
            resizeToScreenSize = true;
        }
    }

    void resize() {
        gameObject.transform.localScale = new Vector3(1, 1, 1);
        topRightScreenCorner = Camera.main.ScreenToWorldPoint(new Vector3 (screenWidth_px, screenHeight_px, Camera.main.transform.position.z));
        screenWidth = topRightScreenCorner.x * 2;
        screenHeight = topRightScreenCorner.y * 2;
        spriteWidth = gameObject.GetComponent<SpriteRenderer>().bounds.size.x;
        spriteHeight = gameObject.GetComponent<SpriteRenderer>().bounds.size.y;
        boxCollider = gameObject.GetComponent<BoxCollider2D>();

        float screenAspectRatio = screenWidth / screenHeight;
        float spriteAspectRatio = spriteWidth / spriteHeight;
        if(screenAspectRatio < spriteAspectRatio) {
            //sprite an screenbreite angleichen
            scalingFactor = screenWidth / spriteWidth;
        } else {
            //sprite an screenhöhe angleichen
            scalingFactor = screenHeight / spriteHeight;
        }

        gameObject.transform.localScale = new Vector3(scalingFactor, scalingFactor, 1);

        Vector2 spriteSize = gameObject.GetComponent<SpriteRenderer>().sprite.bounds.size;
        gameObject.GetComponent<BoxCollider2D>().size = spriteSize;
        gameObject.GetComponent<BoxCollider2D>().offset = new Vector2 (0, 0);
    }

    public float getScaleFactor() {
        return scalingFactor;
    }

    public bool isSpriteResized() {
        return spriteResized;
    }
}
