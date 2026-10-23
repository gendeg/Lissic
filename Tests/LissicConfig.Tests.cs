using System.Diagnostics;

namespace Lissic.Tests;


public class LissicConfigTests
{
	[Fact]
	public async Task SetOptions_BeforeFirstRead_UsesProvidedValues()
	{
		if (Environment.GetEnvironmentVariable("LISSIC_CONFIG_PROBE") == "1")
		{
			LissicConfigurator.SetOptions(new LissicOptions
			{
				RecursionDepthLimit = 12,
				EnableCycleDetection = false
			});
			Assert.Throws<InvalidOperationException>(
				() => LissicConfigurator.SetOptions(new LissicOptions()));

			Assert.Equal((uint)12, LissicConfig.RecursionDepthLimit);
			Assert.False(LissicConfig.EnableCycleDetection);
			return;
		}

		ProcessStartInfo startInfo = new(Environment.ProcessPath!)
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		startInfo.ArgumentList.Add("--filter");
		startInfo.ArgumentList.Add("FullyQualifiedName~SetOptions_BeforeFirstRead_UsesProvidedValues");
		startInfo.Environment["LISSIC_CONFIG_PROBE"] = "1";

		using Process process = Process.Start(startInfo)!;
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;
		Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);
		string standardOutput = await standardOutputTask;
		string standardError = await standardErrorTask;

		Assert.True(process.ExitCode == 0, $"Child test process failed.\n{standardOutput}\n{standardError}");
	}

	[Fact]
	public void FirstRead_UsesOptionDefaults()
	{
		Assert.Equal((uint)64, LissicConfig.RecursionDepthLimit);
		Assert.False(LissicConfig.EnableCycleDetection);
		Assert.Equal(UnknownTagDebugMode.Skip, LissicConfig.UnknownTagDebugMode);
	}

	[Fact]
	public void SetOptions_AfterFirstRead_Throws()
	{
		_ = LissicConfig.RecursionDepthLimit;

		Assert.Throws<InvalidOperationException>(
			() => LissicConfigurator.SetOptions(new LissicOptions()));
	}
}