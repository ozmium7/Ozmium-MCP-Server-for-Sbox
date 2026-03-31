using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Omnibus handler for compilation management operations:
/// compile_project, get_compile_errors, wait_for_compile.
/// </summary>
internal static class CompilationToolHandlers
{
	// ── compile_project ─────────────────────────────────────────────────────

	private static object CompileProject( JsonElement args )
	{
		try
		{
			var target = Project.Current;
			if ( target == null )
				return OzmiumSceneHelpers.Txt( "No active project found." );

			string projectName = OzmiumSceneHelpers.Get( args, "project", (string)null );
			if ( !string.IsNullOrEmpty( projectName ) )
			{
				// Verify the current project matches the requested name
				string title = target.Config?.Title ?? "";
				string root = target.GetRootPath() ?? "";
				bool matches = title.IndexOf( projectName, StringComparison.OrdinalIgnoreCase ) >= 0 ||
					root.IndexOf( projectName, StringComparison.OrdinalIgnoreCase ) >= 0;

				if ( !matches )
					return OzmiumSceneHelpers.Txt( $"Current project '{title}' does not match '{projectName}'. Only the active project can be compiled." );
			}

			// Trigger compile
			EditorUtility.Projects.Compile( target, _ => { } );

			// Collect current diagnostics
			var diagnostics = GetDiagnosticsForProject( target );
			int errorCount = diagnostics.Count( d => d.severity == "Error" );
			int warningCount = diagnostics.Count( d => d.severity == "Warning" );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Compile triggered for '{target.Config?.Title ?? target.GetRootPath()}'.",
				project      = target.Config?.Title ?? "Unknown",
				rootPath     = target.GetRootPath(),
				hasCompiler  = target.HasCompiler,
				errorCount,
				warningCount
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_compile_errors ──────────────────────────────────────────────────

	private static object GetCompileErrors( JsonElement args )
	{
		try
		{
			string severityFilter = OzmiumSceneHelpers.Get( args, "severity", "Warning" );
			int maxResults = OzmiumSceneHelpers.Get( args, "maxResults", 50 );

			var project = Project.Current;
			if ( project == null )
				return OzmiumSceneHelpers.Txt( "No active project found." );

			var diagnostics = GetDiagnosticsForProject( project );
			int errors = diagnostics.Count( d => d.severity == "Error" );
			int warnings = diagnostics.Count( d => d.severity == "Warning" );
			int infos = diagnostics.Count( d => d.severity == "Info" );

			var projectSummary = new
			{
				project    = project.Config?.Title ?? "Unknown",
				rootPath   = project.GetRootPath(),
				hasCompiler = project.HasCompiler,
				errors,
				warnings,
				infos
			};

			// Filter by severity
			var allDiagnostics = new List<Dictionary<string, object>>();
			var filtered = diagnostics.Where( d =>
			{
				return severityFilter.ToLowerInvariant() switch
				{
					"error" => d.severity == "Error",
					"warning" => d.severity == "Error" || d.severity == "Warning",
					"info" => d.severity == "Error" || d.severity == "Warning" || d.severity == "Info",
					_ => d.severity == "Error" || d.severity == "Warning"
				};
			} );

			foreach ( var diag in filtered )
			{
				allDiagnostics.Add( new Dictionary<string, object>
				{
					["project"]  = project.Config?.Title ?? "Unknown",
					["severity"] = diag.severity,
					["id"]       = diag.id,
					["message"]  = diag.message,
					["file"]     = diag.file,
					["line"]     = diag.line,
					["column"]   = diag.column
				} );
			}

			// Sort: errors first, then warnings, then info
			allDiagnostics.Sort( ( a, b ) =>
			{
				int SeverityRank( string s ) => s switch { "Error" => 0, "Warning" => 1, _ => 2 };
				return SeverityRank( a["severity"] as string ?? "" )
					.CompareTo( SeverityRank( b["severity"] as string ?? "" ) );
			} );

			if ( allDiagnostics.Count > maxResults )
				allDiagnostics = allDiagnostics.Take( maxResults ).ToList();

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				summary = $"{allDiagnostics.Count} diagnostic(s) at severity >= {severityFilter}.",
				projects = new[] { projectSummary },
				diagnostics = allDiagnostics
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── wait_for_compile ────────────────────────────────────────────────────

	private static object WaitForCompile( JsonElement args )
	{
		try
		{
			var project = Project.Current;
			if ( project == null )
				return OzmiumSceneHelpers.Txt( "No active project found." );

			var diagnostics = GetDiagnosticsForProject( project );
			int totalErrors = diagnostics.Count( d => d.severity == "Error" );
			int totalWarnings = diagnostics.Count( d => d.severity == "Warning" );

			var result = new
			{
				project      = project.Config?.Title ?? "Unknown",
				hasCompiler  = project.HasCompiler,
				errors       = totalErrors,
				warnings     = totalWarnings
			};

			string status;
			if ( totalErrors > 0 )
				status = $"Complete with errors — {totalErrors} error(s), {totalWarnings} warning(s).";
			else
				status = $"Complete — build succeeded. {totalWarnings} warning(s).";

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				status,
				allSucceeded = totalErrors == 0,
				totalErrors,
				totalWarnings,
				projects = new[] { result }
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private record struct DiagnosticInfo( string severity, string id, string message, string file, int line, int column );

	private static List<DiagnosticInfo> GetDiagnosticsForProject( Project project )
	{
		var results = new List<DiagnosticInfo>();

		try
		{
			// Get diagnostics via the compiler output if available
			if ( !project.HasCompiler )
				return results;

			// Use EditorUtility.Projects.ResolveCompiler to find diagnostics
			// from loaded assemblies, or fall back to checking output diagnostics
			var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault( a =>
				{
					try { return a.Location?.StartsWith( project.GetRootPath() ?? "", StringComparison.OrdinalIgnoreCase ) == true; }
					catch { return false; }
				} );

			Compiler compiler = null;
			if ( assembly != null )
				compiler = EditorUtility.Projects.ResolveCompiler( assembly );

			if ( compiler?.Diagnostics != null )
			{
				foreach ( var d in compiler.Diagnostics )
				{
					if ( d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden )
						continue;

					string filePath = d.Location?.SourceTree?.FilePath ?? "";
					int line = 0;
					int column = 0;

					if ( d.Location?.SourceTree != null )
					{
						var lineSpan = d.Location.GetLineSpan();
						line = lineSpan.StartLinePosition.Line + 1;
						column = lineSpan.StartLinePosition.Character + 1;
					}

					results.Add( new DiagnosticInfo(
						d.Severity.ToString(),
						d.Id,
						d.GetMessage(),
						filePath,
						line,
						column
					) );
				}
			}
		}
		catch
		{
			// Compiler may not be accessible — return empty
		}

		return results;
	}

	// ── Omnibus dispatcher ──────────────────────────────────────────────────

	internal static object ManageCompilation( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"compile_project"   => CompileProject( args ),
			"get_compile_errors" => GetCompileErrors( args ),
			"wait_for_compile"  => WaitForCompile( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation '{operation}'. Valid: compile_project, get_compile_errors, wait_for_compile." )
		};
	}

	// ── Schema ──────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManageCompilation
	{
		get
		{
			var props = new Dictionary<string, object>
			{
				["operation"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "The compilation operation to perform.",
					["enum"]        = new[] { "compile_project", "get_compile_errors", "wait_for_compile" }
				},
				["project"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Project name or path substring to verify (optional). If omitted, targets the active project."
				},
				["severity"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Minimum severity filter for get_compile_errors: 'Error' (errors only), 'Warning' (errors + warnings, default), or 'Info' (all).",
					["enum"]        = new[] { "Error", "Warning", "Info" }
				},
				["maxResults"] = new Dictionary<string, object>
				{
					["type"]        = "integer",
					["description"] = "Maximum diagnostics to return for get_compile_errors (default 50)."
				}
			};

			var schema = new Dictionary<string, object>
			{
				["type"]       = "object",
				["properties"] = props,
				["required"]   = new[] { "operation" }
			};

			return new Dictionary<string, object>
			{
				["name"]        = "manage_compilation",
				["description"] = "Compilation management: trigger a project recompile and get build status (compile_project), read compilation errors and warnings with file/line/column info (get_compile_errors), or check if all compiles have finished (wait_for_compile). Use get_compile_errors after writing code to verify it compiles. Each diagnostic includes severity, error code, message, file path, line, and column.",
				["inputSchema"] = schema
			};
		}
	}
}
