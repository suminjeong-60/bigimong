#pragma once

// Engine-independent male customization selection. IDs match the numbered
// reference sheets (1-15), while the private GLB files start at 00.
#include <string>

namespace BigimongMaleAvatar
{
    enum class Option { EyeShape, FaceShape, HairStyle, SkinTone, EyeColor, HairColor };

    struct Selection
    {
        int eyeShape = 4;
        int faceShape = 1;
        int hairStyle = 1;
        int skinTone = 1;
        int eyeColor = 2;
        int hairColor = 3;
    };

    inline int Limit(Option option)
    {
        switch (option)
        {
        case Option::EyeShape:
        case Option::FaceShape:
        case Option::HairStyle: return 15;
        case Option::SkinTone:
        case Option::EyeColor: return 8;
        case Option::HairColor: return 10;
        }
        return 0;
    }

    inline bool Valid(const Selection& selection)
    {
        return selection.eyeShape >= 1 && selection.eyeShape <= 15 &&
            selection.faceShape >= 1 && selection.faceShape <= 15 &&
            selection.hairStyle >= 1 && selection.hairStyle <= 15 &&
            selection.skinTone >= 1 && selection.skinTone <= 8 &&
            selection.eyeColor >= 1 && selection.eyeColor <= 8 &&
            selection.hairColor >= 1 && selection.hairColor <= 10;
    }

    inline int* Field(Selection& selection, Option option)
    {
        switch (option)
        {
        case Option::EyeShape: return &selection.eyeShape;
        case Option::FaceShape: return &selection.faceShape;
        case Option::HairStyle: return &selection.hairStyle;
        case Option::SkinTone: return &selection.skinTone;
        case Option::EyeColor: return &selection.eyeColor;
        case Option::HairColor: return &selection.hairColor;
        }
        return nullptr;
    }

    inline bool Select(Selection& selection, Option option, int oneBasedId)
    {
        int* field = Field(selection, option);
        if (!field || !Valid(selection) || oneBasedId < 1 || oneBasedId > Limit(option))
            return false;
        *field = oneBasedId;
        return true;
    }

    inline Selection Next(Selection selection, Option option, int offset)
    {
        int* field = Field(selection, option);
        const int limit = Limit(option);
        if (!field || !Valid(selection) || limit == 0) return selection;
        const int wrapped = ((offset % limit) + limit) % limit;
        *field = ((*field - 1 + wrapped) % limit) + 1;
        return selection;
    }

    inline std::string IndexedName(const char* prefix, int oneBasedId)
    {
        if (oneBasedId < 1 || oneBasedId > 15) return {};
        const int zeroBasedId = oneBasedId - 1;
        return std::string(prefix) + (zeroBasedId < 10 ? "0" : "") +
            std::to_string(zeroBasedId);
    }

    inline std::string EyeAssetName(const Selection& selection)
    {
        return IndexedName("male-eye-", selection.eyeShape) + ".glb";
    }

    inline std::string HairAssetName(const Selection& selection)
    {
        return IndexedName("male-hair-", selection.hairStyle) + ".glb";
    }

    inline std::string FaceMorphName(const Selection& selection)
    {
        return IndexedName("face_", selection.faceShape);
    }

    struct Rgb { float red, green, blue; };

    inline Rgb SkinColor(int id)
    {
        constexpr Rgb colors[] = {
            {1.00f, .84f, .72f}, {.96f, .75f, .61f}, {.88f, .64f, .48f},
            {.76f, .51f, .36f}, {.64f, .40f, .27f}, {.51f, .30f, .20f},
            {.39f, .22f, .15f}, {.28f, .15f, .11f}
        };
        return colors[id >= 1 && id <= 8 ? id - 1 : 0];
    }

    inline Rgb IrisColor(int id)
    {
        constexpr Rgb colors[] = {
            {.20f, .11f, .06f}, {.37f, .22f, .10f}, {.52f, .34f, .14f},
            {.16f, .34f, .19f}, {.12f, .31f, .43f}, {.23f, .42f, .58f},
            {.36f, .28f, .50f}, {.36f, .38f, .40f}
        };
        return colors[id >= 1 && id <= 8 ? id - 1 : 0];
    }

    inline Rgb HairColor(int id)
    {
        constexpr Rgb colors[] = {
            {.07f, .05f, .04f}, {.16f, .09f, .06f}, {.28f, .14f, .08f},
            {.43f, .25f, .14f}, {.67f, .46f, .24f}, {.84f, .68f, .38f},
            {.64f, .18f, .12f}, {.16f, .19f, .25f}, {.32f, .18f, .38f},
            {.78f, .73f, .67f}
        };
        return colors[id >= 1 && id <= 10 ? id - 1 : 0];
    }
}
