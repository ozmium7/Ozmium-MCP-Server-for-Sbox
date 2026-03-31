using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Handlers for console-related MCP tools: list_console_commands and run_console_command.
/// </summary>
internal static class ConsoleToolHandlers
{
	// ── list_console_commands ──────────────────────────────────────────────

	internal static object ListConsoleCommands( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string filter = null;
		if ( args.ValueKind != JsonValueKind.Undefined &&
			args.TryGetProperty( "filter", out var fP ) )
			filter = fP.GetString();

		var entries           = new List<Dictionary<string, object>>();
		var skippedAssemblies = new List<string>();

		foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
		{
			try
			{
				foreach ( var type in assembly.GetTypes() )
				{
					foreach ( var prop in type.GetProperties(
						System.Reflection.BindingFlags.Public |
						System.Reflection.BindingFlags.NonPublic |
						System.Reflection.BindingFlags.Static ) )
					{
						var attr = prop.GetCustomAttributes( typeof( ConVarAttribute ), false )
							.FirstOrDefault() as ConVarAttribute;
						if ( attr == null ) continue;

						var cvarName = !string.IsNullOrEmpty( attr.Name )
							? attr.Name
							: prop.Name.ToLowerInvariant();

						if ( !string.IsNullOrEmpty( filter ) &&
							cvarName.IndexOf( filter, StringComparison.OrdinalIgnoreCase ) < 0 )
							continue;

						string currentValue = null;
						try { currentValue = ConsoleSystem.GetValue( cvarName ); } catch { }

						entries.Add( new Dictionary<string, object>
						{
							["name"]         = cvarName,
							["help"]         = attr.Help ?? "",
							["flags"]        = attr.Flags.ToString(),
							["saved"]        = attr.Saved,
							["currentValue"] = currentValue,
							["declaringType"]= type.Name
						} );
					}
				}
			}
			catch ( Exception ex )
			{
				skippedAssemblies.Add( $"{assembly.GetName().Name}: {ex.Message}" );
			}
		}

		entries = entries
			.GroupBy( e => e["name"]?.ToString() )
			.Select( g => g.First() )
			.OrderBy( e => e["name"]?.ToString() )
			.ToList();

		var summary = $"Found {entries.Count} [ConVar] entries" +
			( string.IsNullOrEmpty( filter ) ? "." : $" matching '{filter}'." );
		if ( skippedAssemblies.Count > 0 )
			summary += $" ({skippedAssemblies.Count} assemblies skipped due to reflection errors.)";

		var json = JsonSerializer.Serialize( new { summary, entries, skippedAssemblies }, jsonOptions );
		return OzmiumSceneHelpers.Txt( json );
	}

	// ── run_console_command ────────────────────────────────────────────────

	internal static object RunConsoleCommand( JsonElement args )
	{
		var cmd     = args.GetProperty( "command" ).GetString();
		var parts   = cmd.Trim().Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		var cmdName = parts[0];

		// Only support convars (readable via GetValue). ConCmd methods and unknown
		// commands are rejected with a friendly message — ConsoleSystem.Run throws
		// uncatchable exceptions in s&box's sandbox for both cases.
		string currentValue = null;
		try { currentValue = ConsoleSystem.GetValue( cmdName ); } catch { }

		if ( currentValue == null )
			return OzmiumSceneHelpers.Txt( $"Unknown convar: '{cmdName}'. Only [ConVar] properties are supported. Use list_console_commands to see available names." );

		// Read-only query (no value argument) — just return the current value.
		if ( parts.Length == 1 )
			return OzmiumSceneHelpers.Txt( $"{cmdName} = {currentValue}" );

		// Write: set the convar value using SetValue (no main-thread restriction).
		var newValue = string.Join( " ", parts, 1, parts.Length - 1 );
		ConsoleSystem.SetValue( cmdName, newValue );

		string readback = null;
		try { readback = ConsoleSystem.GetValue( cmdName ); } catch { }

		return OzmiumSceneHelpers.Txt( $"Set {cmdName} = {readback ?? newValue}" );
	}
}
