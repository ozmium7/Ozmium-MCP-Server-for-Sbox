using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;
using Sandbox.Audio;

namespace SboxMcpServer;

/// <summary>
/// Audio MCP tools: create_sound_point, configure_sound.
/// </summary>
internal static class AudioToolHandlers
{
	// ── create_sound_point ───────────────────────────────────────────────────

	internal static object CreateSoundPoint( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Sound Point" );
		string sound = OzmiumSceneHelpers.Get( args, "soundEvent", (string)null );
		float  vol   = OzmiumSceneHelpers.Get( args, "volume", 1f );
		float  pitch = OzmiumSceneHelpers.Get( args, "pitch", 1f );
		bool   play  = OzmiumSceneHelpers.Get( args, "playOnStart", true );
		bool   repeat = OzmiumSceneHelpers.Get( args, "repeat", false );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var snd = go.Components.Create<SoundPointComponent>();
			if ( !string.IsNullOrEmpty( sound ) )
			{
				var asset = AssetSystem.FindByPath( sound );
				if ( asset != null )
				{
					var ev = asset.LoadResource<SoundEvent>();
					if ( ev != null ) snd.SoundEvent = ev;
				}
			}
			snd.Volume = vol;
			snd.Pitch = pitch;
			snd.PlayOnStart = play;
			snd.Repeat = repeat;

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created SoundPoint '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── configure_sound ─────────────────────────────────────────────────────

	internal static object ConfigureSound( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGoWithComponent<BaseSoundComponent>( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"No object with BaseSoundComponent found (id={id ?? "null"}, name={name ?? "null"})." );

		var snd = go.Components.Get<BaseSoundComponent>();

		try
		{
			if ( args.TryGetProperty( "soundEvent", out var seEl ) && seEl.ValueKind == JsonValueKind.String )
			{
				var asset = AssetSystem.FindByPath( seEl.GetString() );
				if ( asset != null )
				{
					var ev = asset.LoadResource<SoundEvent>();
					if ( ev != null ) snd.SoundEvent = ev;
				}
			}
			if ( args.TryGetProperty( "volume", out var vEl ) )
				snd.Volume = vEl.GetSingle();
			if ( args.TryGetProperty( "pitch", out var pEl ) )
				snd.Pitch = pEl.GetSingle();
			if ( args.TryGetProperty( "playOnStart", out var posEl ) )
				snd.PlayOnStart = posEl.GetBoolean();
			if ( args.TryGetProperty( "repeat", out var repEl ) )
				snd.Repeat = repEl.GetBoolean();
			if ( args.TryGetProperty( "distanceAttenuation", out var daEl ) )
				snd.DistanceAttenuation = daEl.GetBoolean();
			if ( args.TryGetProperty( "distance", out var dEl ) )
				snd.Distance = dEl.GetSingle();

			return OzmiumSceneHelpers.Txt( $"Configured sound on '{go.Name}' ({snd.GetType().Name})." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Schemas ─────────────────────────────────────────────────────────────

	private static Dictionary<string, object> S( string name, string desc, Dictionary<string, object> props, string[] req = null )
	{
		var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
		if ( req != null ) schema["required"] = req;
		return new Dictionary<string, object> { ["name"] = name, ["description"] = desc, ["inputSchema"] = schema };
	}

	internal static Dictionary<string, object> SchemaCreateSoundPoint => S( "create_sound_point",
		"Creates a GO with a SoundPointComponent for spatial audio.",
		new Dictionary<string, object>
		{
			["x"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["soundEvent"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sound event path." },
			["volume"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Volume (0-1)." },
			["pitch"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pitch (0-2)." },
			["playOnStart"]   = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Play on start (default true)." },
			["repeat"]       = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Repeat sound." }
		} );

	internal static Dictionary<string, object> SchemaConfigureSound => S( "configure_sound",
		"Configures an existing BaseSoundComponent on a GameObject.",
		new Dictionary<string, object>
		{
			["id"]                  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]                = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["soundEvent"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sound event path." },
			["volume"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Volume (0-1)." },
			["pitch"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pitch (0-2)." },
			["playOnStart"]        = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Play on start." },
			["repeat"]             = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Repeat sound." },
			["distanceAttenuation"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Enable distance attenuation." },
			["distance"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Distance attenuation distance." }
		} );

	// ── create_soundscape_trigger ────────────────────────────────────────────

	internal static object CreateSoundscapeTrigger( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Soundscape Trigger" );
		string triggerType = OzmiumSceneHelpers.Get( args, "type", "Sphere" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var st = go.Components.Create<SoundscapeTrigger>();

			if ( Enum.TryParse<SoundscapeTrigger.TriggerType>( triggerType, true, out var tt ) )
				st.Type = tt;

			st.Volume = OzmiumSceneHelpers.Get( args, "volume", 1.0f );
			st.Radius = OzmiumSceneHelpers.Get( args, "radius", 500f );
			st.StayActiveOnExit = OzmiumSceneHelpers.Get( args, "stayActiveOnExit", true );

			if ( args.TryGetProperty( "boxSize", out var bsEl ) && bsEl.ValueKind == JsonValueKind.Object )
			{
				st.BoxSize = new Vector3(
					OzmiumSceneHelpers.Get( bsEl, "x", 50f ),
					OzmiumSceneHelpers.Get( bsEl, "y", 50f ),
					OzmiumSceneHelpers.Get( bsEl, "z", 50f ) );
			}

			if ( args.TryGetProperty( "soundscapePath", out var spEl ) && spEl.ValueKind == JsonValueKind.String )
			{
				var asset = AssetSystem.FindByPath( spEl.GetString() );
				if ( asset != null )
				{
					var scape = asset.LoadResource<Soundscape>();
					if ( scape != null ) st.Soundscape = scape;
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created SoundscapeTrigger '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				type     = st.Type.ToString(),
				volume   = st.Volume
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_sound_box ─────────────────────────────────────────────────────

	internal static object CreateSoundBox( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Sound Box" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var sb = go.Components.Create<SoundBoxComponent>();
			sb.Volume = OzmiumSceneHelpers.Get( args, "volume", 1f );
			sb.PlayOnStart = OzmiumSceneHelpers.Get( args, "playOnStart", true );
			sb.Repeat = OzmiumSceneHelpers.Get( args, "repeat", false );

			if ( args.TryGetProperty( "boxSize", out var bsEl ) && bsEl.ValueKind == JsonValueKind.Object )
			{
				sb.Scale = new Vector3(
					OzmiumSceneHelpers.Get( bsEl, "x", 50f ),
					OzmiumSceneHelpers.Get( bsEl, "y", 50f ),
					OzmiumSceneHelpers.Get( bsEl, "z", 50f ) );
			}

			if ( args.TryGetProperty( "soundEvent", out var seEl ) && seEl.ValueKind == JsonValueKind.String )
			{
				var asset = AssetSystem.FindByPath( seEl.GetString() );
				if ( asset != null )
				{
					var ev = asset.LoadResource<SoundEvent>();
					if ( ev != null ) sb.SoundEvent = ev;
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created SoundBox '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				boxSize  = new { sb.Scale.x, sb.Scale.y, sb.Scale.z }
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_dsp_volume ────────────────────────────────────────────────────

	internal static object CreateDspVolume( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "DSP Volume" );
		string volumeType = OzmiumSceneHelpers.Get( args, "volumeType", "Box" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var dsp = go.Components.Create<DspVolume>();
			dsp.Priority = OzmiumSceneHelpers.Get( args, "priority", 0 );

			string targetMixer = OzmiumSceneHelpers.Get( args, "targetMixer", "Game" );
			if ( !string.IsNullOrEmpty( targetMixer ) )
				dsp.TargetMixer = new MixerHandle { Name = targetMixer };

			// Configure volume shape
			var sv = dsp.SceneVolume;
			if ( Enum.TryParse<Sandbox.Volumes.SceneVolume.VolumeTypes>( volumeType, true, out var vt ) )
				sv.Type = vt;

			if ( sv.Type == Sandbox.Volumes.SceneVolume.VolumeTypes.Box )
			{
				if ( args.TryGetProperty( "boxSize", out var bsEl ) && bsEl.ValueKind == JsonValueKind.Object )
				{
					sv.Box = BBox.FromPositionAndSize( 0,
						new Vector3(
							OzmiumSceneHelpers.Get( bsEl, "x", 200f ),
							OzmiumSceneHelpers.Get( bsEl, "y", 200f ),
							OzmiumSceneHelpers.Get( bsEl, "z", 200f ) ) );
				}
			}
			else if ( sv.Type == Sandbox.Volumes.SceneVolume.VolumeTypes.Sphere )
			{
				sv.Sphere = new Sphere( 0, OzmiumSceneHelpers.Get( args, "radius", 200f ) );
			}

			dsp.SceneVolume = sv;

			if ( args.TryGetProperty( "dspPreset", out var dpEl ) && dpEl.ValueKind == JsonValueKind.String )
			{
				dsp.Dsp = new DspPresetHandle { Name = dpEl.GetString() };
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created DspVolume '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				volumeType = sv.Type.ToString()
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Audio extension schemas ─────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaCreateSoundscapeTrigger => S( "create_soundscape_trigger",
		"Create a GO with a SoundscapeTrigger for ambient audio zones (outdoor birds, indoor machinery, cave reverb).",
		new Dictionary<string, object>
		{
			["x"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["type"]             = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Trigger type.",
				["enum"] = new[] { "Point", "Sphere", "Box" }
			},
			["soundscapePath"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Soundscape asset path." },
			["volume"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Volume (default 1.0)." },
			["radius"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Radius for Sphere type (default 500)." },
			["boxSize"]          = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Box half-extents {x,y,z} for Box type." },
			["stayActiveOnExit"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Keep playing after exiting (default true)." }
		} );

	internal static Dictionary<string, object> SchemaCreateSoundBox => S( "create_sound_box",
		"Create a GO with a SoundBoxComponent for area ambient sounds (machinery hum, wind in corridor).",
		new Dictionary<string, object>
		{
			["x"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["soundEvent"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sound event path." },
			["volume"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Volume (0-1)." },
			["playOnStart"]  = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Play on start (default true)." },
			["repeat"]       = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Repeat sound." },
			["boxSize"]      = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Box half-extents {x,y,z} (default 50,50,50)." }
		} );

	internal static Dictionary<string, object> SchemaCreateDspVolume => S( "create_dsp_volume",
		"Create a GO with a DspVolume for audio effect zones (reverb in halls, lowpass underwater).",
		new Dictionary<string, object>
		{
			["x"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["dspPreset"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "DSP preset asset path." },
			["targetMixer"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Target mixer name (default 'Game')." },
			["priority"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Priority (default 0)." },
			["volumeType"]   = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Volume shape type.",
				["enum"] = new[] { "Box", "Sphere", "Infinite" }
			},
			["boxSize"]      = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Box size {x,y,z} (default 200,200,200)." },
			["radius"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Sphere radius (default 200)." }
		} );

	// ── create_audio_listener ─────────────────────────────────────────────

	internal static object CreateAudioListener( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Audio Listener" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var listener = go.Components.Create<AudioListener>();
			listener.UseCameraDirection = OzmiumSceneHelpers.Get( args, "useCameraDirection", true );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created AudioListener '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	internal static Dictionary<string, object> SchemaCreateAudioListener => S( "create_audio_listener",
		"Create a GO with an AudioListener for custom audio origin points. Defines where the player 'hears' from — useful for security cameras, cutscenes.",
		new Dictionary<string, object>
		{
			["x"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]               = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["useCameraDirection"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Use camera direction for audio (default true)." }
		} );

	// ── manage_audio (Omnibus) ─────────────────────────────────────────────

	internal static object ManageAudio( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create_sound_point"       => CreateSoundPoint( args ),
			"configure_sound"          => ConfigureSound( args ),
			"create_soundscape_trigger" => CreateSoundscapeTrigger( args ),
			"create_sound_box"         => CreateSoundBox( args ),
			"create_dsp_volume"        => CreateDspVolume( args ),
			"create_audio_listener"    => CreateAudioListener( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: create_sound_point, configure_sound, create_soundscape_trigger, create_sound_box, create_dsp_volume, create_audio_listener" )
		};
	}

	internal static Dictionary<string, object> SchemaManageAudio => S( "manage_audio",
		"Manage audio: create/configure sound points, soundscape triggers, sound boxes, DSP volumes, audio listeners.",
		new Dictionary<string, object>
		{
			["operation"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "create_sound_point", "configure_sound", "create_soundscape_trigger", "create_sound_box", "create_dsp_volume", "create_audio_listener" } },
			["id"]                   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID (for configure operations)." },
			["name"]                 = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["x"]                    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["soundEvent"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sound event path." },
			["volume"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Volume (0-1)." },
			["pitch"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pitch (0-2)." },
			["playOnStart"]          = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Play on start." },
			["repeat"]               = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Repeat sound." },
			["distanceAttenuation"]  = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Enable distance attenuation." },
			["distance"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Distance attenuation distance." },
			["type"]                 = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Trigger type.", ["enum"] = new[] { "Point", "Sphere", "Box" } },
			["soundscapePath"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Soundscape asset path." },
			["radius"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Radius." },
			["boxSize"]              = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Box size {x,y,z}." },
			["stayActiveOnExit"]     = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Keep playing after exiting." },
			["dspPreset"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "DSP preset asset path." },
			["targetMixer"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Target mixer name." },
			["priority"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Priority." },
			["volumeType"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Volume shape type.", ["enum"] = new[] { "Box", "Sphere", "Infinite" } },
			["useCameraDirection"]   = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Use camera direction for audio." }
		},
		new[] { "operation" } );
}
