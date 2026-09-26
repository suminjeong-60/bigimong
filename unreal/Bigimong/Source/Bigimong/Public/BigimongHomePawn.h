#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Pawn.h"
#include "InputCoreTypes.h"
#include "BigimongMaleAvatarCore.h"
#include "BigimongHomePawn.generated.h"

class UCameraComponent;
class UInputComponent;
class USceneComponent;
class UStaticMesh;
class UStaticMeshComponent;
class USkeletalMesh;
class USkeletalMeshComponent;
class UBigimongMaleEditorWidget;

UCLASS()
class BIGIMONG_API ABigimongHomePawn : public APawn
{
    GENERATED_BODY()

public:
    ABigimongHomePawn();
    virtual void BeginPlay() override;
    virtual void Tick(float DeltaSeconds) override;
    virtual void SetupPlayerInputComponent(UInputComponent* PlayerInputComponent) override;

    UFUNCTION(BlueprintCallable, Category="Male Avatar")
    bool SelectMaleOption(int32 Category, int32 OneBasedId);

    UFUNCTION(BlueprintCallable, Category="Male Avatar")
    bool StepMaleOption(int32 Category, int32 Direction);

    UFUNCTION(BlueprintPure, Category="Male Avatar")
    int32 GetMaleOption(int32 Category) const;

    UFUNCTION(BlueprintPure, Category="Male Avatar")
    bool CanCustomizeMale() const { return bMalePartsReady; }

protected:
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Home")
    TObjectPtr<USceneComponent> PreviewPivot;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Home")
    TObjectPtr<UCameraComponent> PreviewCamera;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Home")
    TObjectPtr<UStaticMeshComponent> Egg;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Home")
    TObjectPtr<UStaticMeshComponent> Male;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Home")
    TObjectPtr<UStaticMeshComponent> Female;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Male Avatar")
    TObjectPtr<USceneComponent> MalePartsPivot;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Male Avatar")
    TObjectPtr<UStaticMeshComponent> MaleBody;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Male Avatar")
    TObjectPtr<USkeletalMeshComponent> MaleFace;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Male Avatar")
    TObjectPtr<UStaticMeshComponent> MaleEyes;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Male Avatar")
    TObjectPtr<UStaticMeshComponent> MaleHair;

    // Assign imported static meshes here or import them with these stable names.
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="VARCO")
    TSoftObjectPtr<UStaticMesh> MaleAsset;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="VARCO")
    TSoftObjectPtr<UStaticMesh> FemaleAsset;

    // Import the verified male-base body and its morph-enabled face separately.
    UPROPERTY(EditDefaultsOnly, Category="VARCO|Male")
    TSoftObjectPtr<UStaticMesh> MaleBodyAsset;

    UPROPERTY(EditDefaultsOnly, Category="VARCO|Male")
    TSoftObjectPtr<USkeletalMesh> MaleFaceAsset;

    UPROPERTY(EditDefaultsOnly, Category="VARCO|Male")
    TArray<TSoftObjectPtr<UStaticMesh>> MaleEyeAssets;

    UPROPERTY(EditDefaultsOnly, Category="VARCO|Male")
    TArray<TSoftObjectPtr<UStaticMesh>> MaleHairAssets;

private:
    bool bEggSelected = false;
    bool bFemaleSelected = false;
    bool bMalePartsReady = false;
    BigimongMaleAvatar::Selection MaleSelection;
    int32 ActiveFaceShape = 0;
    UPROPERTY(Transient)
    TObjectPtr<UBigimongMaleEditorWidget> MaleEditor;
    bool bTouchActive = false;
    FVector2D TouchStart = FVector2D::ZeroVector;
    FVector2D LastTouch = FVector2D::ZeroVector;

    void RefreshVisibility();
    void ApplyImportedModel(UStaticMeshComponent* Target, TSoftObjectPtr<UStaticMesh>& Asset, float HeightCm);
    void LoadMaleParts();
    bool ApplyMaleSelection(const BigimongMaleAvatar::Selection& Candidate);
    bool SaveMaleSelection(const BigimongMaleAvatar::Selection& Candidate);
    void LoadMaleSelection();
    void RotatePreview(float Pixels);
    void ToggleSubject();
    void SelectMale();
    void SelectFemale();
    void TouchPressed(ETouchIndex::Type FingerIndex, FVector ScreenLocation);
    void TouchReleased(ETouchIndex::Type FingerIndex, FVector ScreenLocation);
};
