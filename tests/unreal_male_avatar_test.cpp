#include "../unreal/Bigimong/Source/Bigimong/Public/BigimongMaleAvatarCore.h"

#include <cassert>
#include <iostream>

using namespace BigimongMaleAvatar;

int main()
{
    const Selection original;
    assert(Valid(original));
    // The first male preview uses hair 1, face 1, and the large eyes in panel 4.
    assert(original.hairStyle == 1 && original.faceShape == 1 && original.eyeShape == 4);
    assert(EyeAssetName(original) == "male-eye-03.glb");
    assert(HairAssetName(original) == "male-hair-00.glb");
    assert(FaceMorphName(original) == "face_00");

    Selection changed = original;
    assert(Select(changed, Option::EyeShape, 15));
    assert(Select(changed, Option::HairStyle, 15));
    assert(Select(changed, Option::FaceShape, 15));
    assert(EyeAssetName(changed) == "male-eye-14.glb");
    assert(HairAssetName(changed) == "male-hair-14.glb");
    assert(FaceMorphName(changed) == "face_14");
    assert(Next(changed, Option::HairStyle, 1).hairStyle == 1);
    assert(Next(original, Option::FaceShape, -1).faceShape == 15);
    assert(Next(original, Option::EyeShape, -1).eyeShape == 3);

    assert(Select(changed, Option::SkinTone, 8));
    assert(Select(changed, Option::EyeColor, 8));
    assert(Select(changed, Option::HairColor, 10));
    assert(Valid(changed));
    assert(!Select(changed, Option::EyeShape, 0));
    assert(!Select(changed, Option::FaceShape, 16));
    assert(!Select(changed, Option::SkinTone, 9));
    assert(!Select(changed, Option::HairColor, 11));
    assert(EyeAssetName(changed) == "male-eye-14.glb");
    assert(Next(changed, Option::SkinTone, 1).skinTone == 1);
    const auto skin = SkinColor(original.skinTone);
    const auto iris = IrisColor(original.eyeColor);
    const auto hair = HairColor(original.hairColor);
    assert(skin.red == 1.0f && skin.green == 0.84f && skin.blue == 0.72f);
    assert(iris.red == 0.37f && iris.green == 0.22f && iris.blue == 0.10f);
    assert(hair.red == 0.28f && hair.green == 0.14f && hair.blue == 0.08f);

    std::cout << "Unreal male avatar selection: passed\n";
}
