using System.Linq;
using Content.Shared._NF.BountyContracts;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._NF.BountyContracts.UI;

[GenerateTypedNameReferences]
public sealed partial class BountyContractUiFragmentCreate : Control
{
    [Dependency] IPrototypeManager _proto = default!;
    public event Action<BountyContractRequest>? OnCreatePressed;
    public event Action? OnCancelPressed;

    private List<BountyContractTargetInfo> _targets = new();
    private List<string> _vessels = new();
    private ProtoId<BountyContractCollectionPrototype> _collection;

    public BountyContractUiFragmentCreate(ProtoId<BountyContractCollectionPrototype> collection)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        CategorySelector.OnItemSelected += opt => OnCategorySelected(opt.Id);
        NameSelector.OnItemSelected += opt => OnNameSelected(opt.Id);
        VesselSelector.OnItemSelected += opt => OnVesselSelected(opt.Id);

        CustomNameButton.OnToggled += args => OnCustomNameToggle(args.Pressed);
        CustomVesselButton.OnToggled += args => OnCustomVesselToggle(args.Pressed);
        NameEdit.OnTextChanged += _ => UpdateDisclaimer();
        RewardEdit.OnTextChanged += _ => UpdateDisclaimer();
        VesselEdit.OnTextChanged += _ => UpdateDisclaimer();

        var descPlaceholder = Loc.GetString("bounty-contracts-ui-create-description-placeholder");
        DescriptionEdit.Placeholder = new Rope.Leaf(descPlaceholder);
        DescriptionEdit.OnTextChanged += _ => UpdateDisclaimer();
        RewardEdit.Text = SharedBountyContractSystem.DefaultReward.ToString();

        CreateButton.OnPressed += _ => OnCreatePressed?.Invoke(GetBountyContract());
        CancelButton.OnPressed += _ => OnCancelPressed?.Invoke();

        _collection = collection;

        FillCategories();
        UpdateDisclaimer();
    }

    public void SetPossibleTargets(List<BountyContractTargetInfo> targets)
    {
        // make sure that all targets sorted by names alphabetically
        _targets = targets.OrderBy(target => target.Name).ToList();

        // update names dropdown
        NameSelector.Clear();
        for (var i = 0; i < _targets.Count; i++)
        {
            NameSelector.AddItem(_targets[i].Name, i);
        }

        // set selector to first option
        OnNameSelected(0);
    }

    public void SetVessels(List<string> vessels)
    {
        // make sure that all ships sorted by names alphabetically
        vessels.Sort();

        // add unknown option as a first option
        vessels.Insert(0, Loc.GetString("bounty-contracts-ui-create-vessel-unknown"));
        _vessels = vessels;

        // update ships dropdown
        VesselSelector.Clear();
        for (var i = 0; i < _vessels.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(_vessels[i]))
                continue;

            VesselSelector.AddItem(_vessels[i], i);
        }

        // set vessel to unknown
        OnVesselSelected(0);
    }

    private void FillCategories()
    {
        if (!_proto.TryIndex(_collection, out var collectionProto))
            return;

        foreach (var id in collectionProto.Categories)
        {
            if (!SharedBountyContractSystem.CategoriesMeta.ContainsKey(id))
                continue;
            var meta = SharedBountyContractSystem.CategoriesMeta[id];
            var name = Loc.GetString(meta.Name);
            CategorySelector.AddItem(name, (int)id);
        }
    }

    private void UpdateDna(string? dnaStr)
    {
        if (string.IsNullOrEmpty(dnaStr))
        {
            DnaBox.Visible = false;
            return;
        }

        DnaBox.Visible = true;
        DnaLabel.Text = dnaStr;
    }

    private void OnNameSelected(int itemIndex)
    {
        if (itemIndex >= _targets.Count)
            return;

        NameSelector.SelectId(itemIndex);

        // update dna
        var selectedTarget = _targets[itemIndex];
        var dnaStr = selectedTarget.DNA;
        UpdateDna(dnaStr);

        UpdateDisclaimer();
    }

    private void OnVesselSelected(int itemIndex)
    {
        if (itemIndex >= _vessels.Count)
            return;

        VesselSelector.SelectId(itemIndex);
    }

    private void OnCategorySelected(int objId)
    {
        var cat = (BountyContractCategory)objId;
        CustomNameButton.Pressed = cat != BountyContractCategory.Criminal;
        OnCustomNameToggle(CustomNameButton.Pressed);

        CategorySelector.SelectId(objId);
    }

    private void OnCustomNameToggle(bool isPressed)
    {
        NameSelector.Visible = !isPressed;
        NameEdit.Visible = isPressed;

        UpdateDna(GetTargetDna());
        UpdateDisclaimer();
    }

    private void OnCustomVesselToggle(bool isPressed)
    {
        VesselSelector.Visible = !isPressed;
        VesselEdit.Visible = isPressed;

        OnVesselSelected(0);
    }

    private void UpdateDisclaimer()
    {
        // check if reward is valid
        var reward = GetReward();
        if (reward == null || reward < 0)
        {
            var err = Loc.GetString("bounty-contracts-ui-create-error-invalid-price");
            DisclaimerLabel.SetMessage(err);
            CreateButton.Disabled = true;
            return;
        }

        // check if name is valid
        var name = GetTargetName();
        if (name == "")
        {
            var err = Loc.GetString("bounty-contracts-ui-create-error-no-name");
            DisclaimerLabel.SetMessage(err);
            CreateButton.Disabled = true;
            return;
        }

        if (name.Length > SharedBountyContractSystem.MaxNameLength)
        {
            var err = Loc.GetString("bounty-contracts-ui-create-error-name-too-long");
            DisclaimerLabel.SetMessage(err);
            CreateButton.Disabled = true;
            return;
        }

        if (VesselEdit.Text.Length > SharedBountyContractSystem.MaxVesselLength)
        {
            var err = Loc.GetString("bounty-contracts-ui-create-error-vessel-name-too-long");
            DisclaimerLabel.SetMessage(err);
            CreateButton.Disabled = true;
            return;
        }

        if (DescriptionEdit.TextLength > SharedBountyContractSystem.MaxDescriptionLength)
        {
            var err = Loc.GetString("bounty-contracts-ui-create-error-description-too-long");
            DisclaimerLabel.SetMessage(err);
            CreateButton.Disabled = true;
            return;
        }

        // all looks good
        DisclaimerLabel.SetMessage(Loc.GetString("bounty-contracts-ui-create-ready"));
        CreateButton.Disabled = false;
    }

    public int? GetReward()
    {
        var priceStr = RewardEdit.Text;
        return int.TryParse(priceStr, out var price) ? price : null;
    }

    public BountyContractTargetInfo? GetTargetInfo()
    {
        BountyContractTargetInfo? info = null;
        if (!CustomNameButton.Pressed)
        {
            var id = NameSelector.SelectedId;
            if (id < _targets.Count)
                info = _targets[id];
        }
        else
        {
            info = new BountyContractTargetInfo
            {
                Name = NameEdit.Text,
                DNA = null
            };
        }

        return info;
    }

    public string GetTargetName()
    {
        var info = GetTargetInfo();
        return info != null ? info.Value.Name : "";
    }

    public string? GetTargetDna()
    {
        var info = GetTargetInfo();
        return info?.DNA;
    }

    public string GetVessel()
    {
        var vessel = "";

        if (!CustomVesselButton.Pressed)
        {
            var id = VesselSelector.SelectedId;
            if (id < _vessels.Count)
                vessel = _vessels[id];
        }
        else
        {
            vessel = VesselEdit.Text;
        }

        return vessel;
    }

    public BountyContractCategory GetCategory()
    {
        return (BountyContractCategory)CategorySelector.SelectedId;
    }

    public BountyContractRequest GetBountyContract()
    {
        var info = new BountyContractRequest
        {
            Collection = _collection,
            Category = GetCategory(),
            Name = GetTargetName(),
            DNA = GetTargetDna(),
            Vessel = GetVessel(),
            Description = Rope.Collapse(DescriptionEdit.TextRope),
            Reward = GetReward() ?? 0
        };
        return info;
    }
}
