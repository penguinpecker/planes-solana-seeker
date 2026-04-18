using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Headless Android release build for the Solana dApp Store.
// Invoke from CLI (no GUI needed if the Android module is installed):
//
//   /Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity \
//       -batchmode -quit -nographics \
//       -projectPath "/Users/penguinpecker/Downloads/Planes/MissileFinal" \
//       -buildTarget Android \
//       -executeMethod BuildScript.BuildAndroid \
//       -logFile -
//
// Provide keystore credentials via env vars so they're never committed:
//   PLANES_KEYSTORE_PATH       absolute path to dappstore.keystore
//   PLANES_KEYSTORE_PASS       keystore password
//   PLANES_KEYALIAS_NAME       alias (default: dappstore)
//   PLANES_KEYALIAS_PASS       alias password
//   PLANES_APK_OUTPUT          absolute output path (default: Builds/planes-dappstore.apk)
public static class BuildScript
{
    public static void BuildAndroid()
    {
        string keystorePath = Environment.GetEnvironmentVariable("PLANES_KEYSTORE_PATH");
        string keystorePass = Environment.GetEnvironmentVariable("PLANES_KEYSTORE_PASS");
        string keyAlias = Environment.GetEnvironmentVariable("PLANES_KEYALIAS_NAME") ?? "dappstore";
        string keyPass = Environment.GetEnvironmentVariable("PLANES_KEYALIAS_PASS");
        string outputPath = Environment.GetEnvironmentVariable("PLANES_APK_OUTPUT")
                             ?? Path.Combine(Directory.GetCurrentDirectory(), "Builds", "planes-dappstore.apk");

        if (string.IsNullOrEmpty(keystorePath) || string.IsNullOrEmpty(keystorePass) || string.IsNullOrEmpty(keyPass))
        {
            throw new BuildFailedException(
                "Missing keystore credentials. Set PLANES_KEYSTORE_PATH, PLANES_KEYSTORE_PASS, and PLANES_KEYALIAS_PASS.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        // Solana dApp Store requires a signed release APK; debug builds are rejected.
        // Unity 6 keeps the signing creds on PlayerSettings.Android; setting them
        // programmatically here in addition to the -keystorePath/-keystorePass CLI
        // args ensures the settings are live during the BuildPipeline call.
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystorePath;
        PlayerSettings.Android.keystorePass = keystorePass;
        PlayerSettings.Android.keyaliasName = keyAlias;
        PlayerSettings.Android.keyaliasPass = keyPass;

        // 64-bit ARM is required; IL2CPP is required to ship ARM64.
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.development = false;

        string[] scenes = {"Assets/Scenes/SampleScene.unity"};

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"[BuildScript] Result: {summary.result}");
        Debug.Log($"[BuildScript] Output: {outputPath}");
        Debug.Log($"[BuildScript] Size: {summary.totalSize} bytes");
        Debug.Log($"[BuildScript] Errors: {summary.totalErrors}, Warnings: {summary.totalWarnings}");

        if (summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Build failed with result: {summary.result}");
        }
    }

    // Emulator smoke build: signs with Unity's default debug keystore so we
    // don't need the dApp Store credentials. Output defaults to
    // Builds/planes-debug.apk. Not shippable.
    public static void BuildAndroidDebug()
    {
        string outputPath = Environment.GetEnvironmentVariable("PLANES_APK_OUTPUT")
                             ?? Path.Combine(Directory.GetCurrentDirectory(), "Builds", "planes-debug.apk");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

        EditorUserBuildSettings.buildAppBundle = false;
        // development=true so `adb run-as` works for editing PlayerPrefs on the emulator.
        EditorUserBuildSettings.development = true;

        string[] scenes = {"Assets/Scenes/SampleScene.unity"};

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"[BuildScript] (debug) Result: {summary.result}");
        Debug.Log($"[BuildScript] (debug) Output: {outputPath}");
        Debug.Log($"[BuildScript] (debug) Size: {summary.totalSize} bytes");

        if (summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Debug build failed with result: {summary.result}");
        }
    }
}
