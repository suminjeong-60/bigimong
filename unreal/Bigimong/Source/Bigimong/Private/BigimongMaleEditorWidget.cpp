#include "BigimongMaleEditorWidget.h"
#include "BigimongHomePawn.h"
#include "Blueprint/WidgetTree.h"
#include "Components/Border.h"
#include "Components/Button.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"

namespace
{
    UButton* MakeButton(UWidgetTree* Tree, const TCHAR* Caption)
    {
        UButton* Button = Tree->ConstructWidget<UButton>(UButton::StaticClass());
        UTextBlock* Text = Tree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
        Text->SetText(FText::FromString(Caption));
        Text->SetColorAndOpacity(FSlateColor(FLinearColor::Black));
        Button->AddChild(Text);
        return Button;
    }
}

void UBigimongMaleEditorWidget::SetTarget(ABigimongHomePawn* InTarget)
{
    Target = InTarget;
    Refresh();
}

void UBigimongMaleEditorWidget::NativeConstruct()
{
    Super::NativeConstruct();
    UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
    WidgetTree->RootWidget = Canvas;
    Canvas->SetVisibility(ESlateVisibility::SelfHitTestInvisible);

    UBorder* Panel = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
    Panel->SetBrushColor(FLinearColor(.035f, .045f, .07f, .87f));
    Panel->SetPadding(FMargin(14.f));
    UCanvasPanelSlot* Slot = Canvas->AddChildToCanvas(Panel);
    Slot->SetAnchors(FAnchors(0.f, 1.f, 0.f, 1.f));
    Slot->SetAlignment(FVector2D(0.f, 1.f));
    Slot->SetPosition(FVector2D(20.f, -42.f));
    Slot->SetAutoSize(true);

    UVerticalBox* Column = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
    Panel->SetContent(Column);
    UTextBlock* Title = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
    Title->SetText(FText::FromString(TEXT("남자 캐릭터 꾸미기")));
    Title->SetColorAndOpacity(FSlateColor(FLinearColor::White));
    Column->AddChildToVerticalBox(Title);

    UButton* ChangeCategory = MakeButton(WidgetTree, TEXT("항목 바꾸기"));
    ChangeCategory->OnClicked.AddDynamic(this, &UBigimongMaleEditorWidget::NextCategory);
    Column->AddChildToVerticalBox(ChangeCategory);

    CategoryLabel = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
    CategoryLabel->SetColorAndOpacity(FSlateColor(FLinearColor::White));
    Column->AddChildToVerticalBox(CategoryLabel);
    OptionLabel = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
    OptionLabel->SetColorAndOpacity(FSlateColor(FLinearColor::White));
    Column->AddChildToVerticalBox(OptionLabel);

    UHorizontalBox* Actions = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
    Column->AddChildToVerticalBox(Actions);
    PreviousButton = MakeButton(WidgetTree, TEXT("◀ 이전"));
    NextButton = MakeButton(WidgetTree, TEXT("다음 ▶"));
    PreviousButton->OnClicked.AddDynamic(this, &UBigimongMaleEditorWidget::PreviousOption);
    NextButton->OnClicked.AddDynamic(this, &UBigimongMaleEditorWidget::NextOption);
    Actions->AddChildToHorizontalBox(PreviousButton);
    Actions->AddChildToHorizontalBox(NextButton);
    Refresh();
}

void UBigimongMaleEditorWidget::Refresh()
{
    if (!CategoryLabel || !OptionLabel) return;
    static const TCHAR* Names[] = {
        TEXT("눈 모양"), TEXT("얼굴형"), TEXT("머리 모양"),
        TEXT("피부색"), TEXT("눈 색"), TEXT("머리색")};
    const auto Option = static_cast<BigimongMaleAvatar::Option>(Category);
    CategoryLabel->SetText(FText::FromString(Names[Category]));
    const bool bReady = Target && Target->CanCustomizeMale();
    OptionLabel->SetText(FText::FromString(bReady
        ? FString::Printf(TEXT("%d / %d"), Target->GetMaleOption(Category), BigimongMaleAvatar::Limit(Option))
        : TEXT("3D 부품을 가져온 뒤 선택 가능")));
    if (PreviousButton) PreviousButton->SetIsEnabled(bReady);
    if (NextButton) NextButton->SetIsEnabled(bReady);
}

void UBigimongMaleEditorWidget::NextCategory()
{
    Category = (Category + 1) % 6;
    Refresh();
}

void UBigimongMaleEditorWidget::PreviousOption()
{
    if (Target) Target->StepMaleOption(Category, -1);
    Refresh();
}

void UBigimongMaleEditorWidget::NextOption()
{
    if (Target) Target->StepMaleOption(Category, 1);
    Refresh();
}
