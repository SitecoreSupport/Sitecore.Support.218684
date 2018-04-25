namespace Sitecore.Support.Pipelines.HttpRequest
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Sitecore.Abstractions;
  using Sitecore.Configuration;
  using Sitecore.Data.ItemResolvers;
  using Sitecore.Data.Items;
  using Sitecore.DependencyInjection;
  using Sitecore.Diagnostics;
  using Sitecore.IO;
  using Sitecore.SecurityModel;
  using Sitecore.Sites;
  using Sitecore.StringExtensions;
  using Sitecore.Web;
  using Sitecore.Pipelines.HttpRequest;

  public class ItemResolver : Sitecore.Pipelines.HttpRequest.ItemResolver
  {
    #region Constructors
    [Obsolete("Please use another constructor with parameters")]
    public ItemResolver() : base()
    {
    }

    public ItemResolver(BaseItemManager itemManager, ItemPathResolver pathResolver) : base(itemManager, pathResolver)
    {
    }

    protected ItemResolver([NotNull] BaseItemManager itemManager, [NotNull] ItemPathResolver pathResolver, MixedItemNameResolvingMode itemNameResolvingMode) :
      base (itemManager, pathResolver, itemNameResolvingMode)
    {
    }
    #endregion

    public override void Process([NotNull] HttpRequestArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      if (this.SkipItemResolving(args))
      {
        return;
      }

      bool permissionDenied = false;
      Item resolvedItem = null;

      string path = string.Empty;

      try
      {
        this.StartProfilingOperation("Resolve current item.", args);

        Sitecore.Context.Items["sc_Support_218684_ItemResolved"] = false;

        var uniquePaths = new HashSet<string>();
        foreach (var candidatePath in this.GetCandidatePaths(args))
        {
          if (!uniquePaths.Add(candidatePath))
          {
            continue; // Already checked this URL
          }

          if (this.TryResolveItem(candidatePath, args, out resolvedItem, out permissionDenied))
          {
            path = candidatePath; // found matching item by path, will stop search
            break;
          }

          if (permissionDenied)
          {
            return; // found item exists, but we cannot touch it due to lack of permissions.
          }

          var ItemWasResolved = (bool) (Sitecore.Context.Items["sc_Support_218684_ItemResolved"] ?? false);

          if (ItemWasResolved)
          {
            return; // found item exists, but there is no version and EnforceVersionPresence is enabled.
          }
        }

        var site = Sitecore.Context.Site;

        if (resolvedItem == null || resolvedItem.Name.Equals("*"))
        {
          var displayNameItem = this.ResolveByMixedDisplayName(args, out permissionDenied);
          if (displayNameItem != null)
          {
            resolvedItem = displayNameItem;
          }
        }

        if (resolvedItem == null && site != null && !permissionDenied && this.UseSiteStartPath(args))
        {
          if (this.TryResolveItem(site.StartPath, args, out resolvedItem, out permissionDenied))
          {
            path = site.StartPath;
          }
        }
      }
      finally
      {
        if (resolvedItem != null)
        {
          this.TraceInfo("Current item is {0}.".FormatWith(path));
        }

        args.PermissionDenied = permissionDenied;
        Sitecore.Context.Item = resolvedItem;

        this.EndProfilingOperation(status: null, args: args);
      }
    }
  }
}