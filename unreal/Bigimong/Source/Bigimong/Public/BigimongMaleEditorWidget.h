#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "BigimongMaleEditorWidget.generated.h"

class ABigimongHomePawn;
class UButton;
class UImage;
class UTextBlock;

UCLASS()
class BIGIMONG_API UBigimongMaleEditorWidget : public UUserWidget
{
    GENERATED_BODY()

public:
    void SetTarget(ABigimongHomePawn* InTarget);
    void Refresh();

protected:
    virtual void NativeConstruct() override;

private:
    UPROPERTY(Transient)
    TObjectPtr<ABigimongHomePawn> Target;

    UPROPERTY(Transient)
    TObjectPtr<UTextBlock> CategoryLabel;

    UPROPERTY(Transient)
    TObjectPtr<UTextBlock> OptionLabel;

    UPROPERTY(Transient)
    TObjectPtr<UImage> OptionImage;

    UPROPERTY(Transient)
    TObjectPtr<UButton> PreviousButton;

    UPROPERTY(Transient)
    TObjectPtr<UButton> NextButton;

    int32 Category = 0;

    UFUNCTION() void NextCategory();
    UFUNCTION() void PreviousOption();
    UFUNCTION() void NextOption();
};
