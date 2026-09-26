#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Pawn.h"
#include "InputCoreTypes.h"
#include "BigimongHomePawn.generated.h"

class UCameraComponent;
class UInputComponent;
class USceneComponent;
class UStaticMesh;
class UStaticMeshComponent;

UCLASS()
class BIGIMONG_API ABigimongHomePawn : public APawn
{
    GENERATED_BODY()

public:
    ABigimongHomePawn();
    virtual void BeginPlay() override;
    virtual void Tick(float DeltaSeconds) override;
    virtual void SetupPlayerInputComponent(UInputComponent* PlayerInputComponent) override;

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

    // Assign imported static meshes here or import them with these stable names.
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="VARCO")
    TSoftObjectPtr<UStaticMesh> MaleAsset;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="VARCO")
    TSoftObjectPtr<UStaticMesh> FemaleAsset;

private:
    bool bEggSelected = false;
    bool bFemaleSelected = true;
    bool bTouchActive = false;
    FVector2D TouchStart = FVector2D::ZeroVector;
    FVector2D LastTouch = FVector2D::ZeroVector;

    void RefreshVisibility();
    void ApplyImportedModel(UStaticMeshComponent* Target, TSoftObjectPtr<UStaticMesh>& Asset, float HeightCm);
    void RotatePreview(float Pixels);
    void ToggleSubject();
    void SelectMale();
    void SelectFemale();
    void TouchPressed(ETouchIndex::Type FingerIndex, FVector ScreenLocation);
    void TouchReleased(ETouchIndex::Type FingerIndex, FVector ScreenLocation);
};
