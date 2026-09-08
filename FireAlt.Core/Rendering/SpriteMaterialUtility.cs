using UnityEngine;

namespace FireAlt.Core.Rendering
{
    public static class SpriteMaterialUtility
    {
        private static readonly SecondarySpriteTexture[] Buffer = new SecondarySpriteTexture[64];

        public static Material CloneFromLookup(MaterialLookup lookup)
        {
            return lookup.Sprite != default 
                ? Clone(lookup.SrcMaterial, lookup.Sprite, lookup.Texture) 
                : Clone(lookup.SrcMaterial, lookup.Texture);
        }

        private static Material Clone(Material srcMaterial, Sprite sprite, Texture texture)
        {
            var mat = Clone(srcMaterial, texture);
            SetSecondaryTextures(sprite, mat);

            return mat;
        }

        private static Material Clone(Material srcMaterial, Texture texture)
        {
            var mat = new Material(srcMaterial)
            {
                mainTexture = texture
            };
            return mat;
        }
        
        private static void SetSecondaryTextures(Sprite sprite, Material mat)
        {
            var count = sprite.GetSecondaryTextures(Buffer);
            for (int i = 0; i < count; i++)
            {
                var secondaryTexture = Buffer[i];

                if (mat.HasTexture(secondaryTexture.name))
                {
                    mat.SetTexture(secondaryTexture.name, secondaryTexture.texture);
                }
            }
        }
    }
}