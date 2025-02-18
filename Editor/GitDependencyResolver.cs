using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using System.IO;
using System.Text;
using UnityEditor.PackageManager.Requests;


namespace UnityCTVisualizer {
  public class GitDependency {
    public string Name {get; set;}
    public string Url {get; set;}
  }

  [InitializeOnLoad]
  ///<summary>
  ///   Adds the option to add git dependencies to a package. This is only needed because Unity's "package manager"
  //    sucks and doesn't resolve Git dependencies... yeap... you heard that right.
  ///   see: https://discussions.unity.com/t/custom-package-with-git-dependencies/732000
  ///</summary>
  public class GitDependencyResolver {
    private static AddAndRemoveRequest s_request;
    static GitDependencyResolver() {
      Events.registeredPackages += OnRegisteredPackages;
    }

    private static void OnRegisteredPackages(PackageRegistrationEventArgs packageRegistrationEventArgs) {
      List<GitDependency> dependencies = new();
      foreach (var package in packageRegistrationEventArgs.added) {
        List<GitDependency> package_dependencies = GetPackageDependencies(package);
        if (package_dependencies != null) dependencies.AddRange(package_dependencies);
      }
      // remove duplicates
      dependencies = dependencies.Distinct().ToList();
      PackageInfo[] installed_packages = PackageInfo.GetAllRegisteredPackages();
      dependencies.RemoveAll((dependency) => {
        foreach (PackageInfo package in installed_packages) {
          try {
            string git_url = package.packageId.Split("@").ElementAt(1);
            if (git_url == dependency.Url) return true;
          }
          catch (System.ArgumentOutOfRangeException) {
            continue;
          }
        }
        return false;
      });
      // do nothing if no new dependencies are detected
      if (dependencies.Count == 0) return;
      // finally install the dependencies
      InstallDependencies(dependencies);
    }

    private static List<GitDependency> GetPackageDependencies(PackageInfo packageInfo) {
      string package_json_path = Path.Join(packageInfo.resolvedPath, "package.json");
      JToken git_dependencies_obj = JObject.Parse(File.ReadAllText(package_json_path))["git-dependencies"];
      if (git_dependencies_obj == null) return null;
      List<JToken> results = git_dependencies_obj.Children().ToList();
      List<GitDependency> git_dependencies = new (results.Count);
      foreach (JToken result in results) {
        GitDependency dependency = result.ToObject<GitDependency>();
        git_dependencies.Add(dependency);
      }
      return git_dependencies;
    }

    private static void InstallDependencies(IEnumerable<GitDependency> dependencies) {
      if (!Application.isBatchMode && !EditorUtility.DisplayDialog(
        "GIT Package Dependency Resolver",
        $"The following packages are required and going to be fetched then installed: {Prettify(dependencies)}",
        "Fetch and Install Dependencies",
        "Cancel"
      )) return;
      string[] git_urls = dependencies.Select(d => d.Url).ToArray();
      s_request = Client.AddAndRemove(git_urls, null);
      EditorUtility.DisplayProgressBar("GIT Package Dependency Resolver",
       "fetching and installing git dependencies ...", 0.0f);
      EditorApplication.update += RunTaskOnUpdate;
    }

    private static string Prettify(IEnumerable<GitDependency> dependencies) {
      StringBuilder sb = new();
      foreach (GitDependency dependency in dependencies) {
        sb.AppendFormat("\n\nname: {0}\nurl: {1}", dependency.Name, dependency.Url);
      }
      return sb.ToString();
    }

    private static void RunTaskOnUpdate() {
      if (s_request.IsCompleted) {
        EditorUtility.ClearProgressBar();
        EditorApplication.update -= RunTaskOnUpdate;
      }
    }
  }
}