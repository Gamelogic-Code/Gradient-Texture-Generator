// Author: Herman Tulleken
// www.gamelogic.co.za

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using Random = UnityEngine.Random;

namespace Gamelogic.Experimental.Tools.Editor
{
	/// <summary>
	/// Wraps a list of colors in a <see cref="ScriptableObject"/>.
	/// </summary>
	/*	Design Note: This class is used to wrap a list of colors so we get the nice
		reorderable list in the editor window. 
	*/
	[Serializable]
	public class ColorList : ScriptableObject
	{
		/// <summary>
		/// The wrapped list of colors.
		/// </summary>
		[ColorUsage(false, false)]
		public List<Color> colors = new()
		{
			Color.black,
			Color.white
		};
		
		/// <summary>
		/// The field name of the colors list.
		/// </summary>
		/*	Design Note: This property is used to get the name of the colors field
			so we can get the property of the serialized object by name. 
		*/
		public string ColorsFieldName => nameof(colors);

		/// <summary>
		/// Calculates a new color interpreting the list of colors as an evenly spaced linear gradient.
		/// </summary>
		/// <param name="t">The interpolation parameter.</param>
		/// <returns>
		/// If the list has one color, that color is returned. If the list has two colors, the linear
		/// interpolation between the two colors is returned. If the list has more than two colors, the
		/// linear interpolation between the two colors that t falls between is returned. For example, if there are three
		/// colors and t = 0.25, the return color is the color midway between the first and second colors. 
		/// </returns>
		public Color Evaluate(float t)
		{
			if (colors.Count == 0)
			{
				return Color.white;
			}

			t = Mathf.Clamp01(t);

			float scaledIndex = t * (colors.Count - 1);
			int lowerIndex = Mathf.FloorToInt(scaledIndex);
			int upperIndex = Mathf.CeilToInt(scaledIndex);

			lowerIndex = Mathf.Clamp(lowerIndex, 0, colors.Count - 1);
			upperIndex = Mathf.Clamp(upperIndex, 0, colors.Count - 1);

			float fraction = scaledIndex - lowerIndex;

			return Color.Lerp(colors[lowerIndex], colors[upperIndex], fraction);
		}
	}
	
	/// <summary>
	/// Thrown when looking for a property (for example, in a serialized object) and the property is not found.
	/// </summary>
	// Reuse candidate
	public class PropertyNotfoundException : Exception
	{
		public PropertyNotfoundException(string propertyName, SerializedObject property) 
			: base($"Property {propertyName} not found in {property}.")
		{}
	}
	
	/// <summary>
	/// Provides extension methods for <see cref="SerializedProperty"/>.
	/// </summary>
	// Reuse candidate
	public static class SerializedPropertyExtensions
	{
		/// <summary>
		/// Trys to find a property in a serialized object. If the property is not found, an exception is thrown.
		/// </summary>
		/// <param name="serializedObject">The serialized object to search in.</param>
		/// <param name="propertyName">The name of the property to find.</param>
		/// <returns>The found property.</returns>
		/// <exception cref="PropertyNotfoundException">The property was not found.</exception>
		public static SerializedProperty FindRequiredProperty(this SerializedObject serializedObject, string propertyName)
		{
			var property = serializedObject.FindProperty(propertyName);
			if (property == null)
			{
				throw new PropertyNotfoundException(propertyName, serializedObject);
			}

			return property;
		}
	}

	/// <summary>
	/// A window that can be use as a base class for texture generation windows. See the code of
	/// <see cref="RampTextureGenerator"/> for an example of how to implement a texture generation window.
	/// </summary>
	public abstract class TextureGeneratorWindow : EditorWindow
	{
		private const int DefaultImageSize = 256;
		private static readonly Vector2Int PreviewImageSizeMax = new(256, 256);
		
		private Vector2Int imageDimensions = new(DefaultImageSize, DefaultImageSize);
		private Texture2D previewTexture;
		
		Vector2Int PreviewDimensions => Vector2Int.Min(PreviewImageSizeMax, imageDimensions);
		
		public void OnGUI()
		{
			EditorGUI.BeginChangeCheck();
			DrawPropertiesGui();
			DrawImageSizeControls(ref imageDimensions);

			if (EditorGUI.EndChangeCheck())
			{
				GeneratePreviewTexture();
			}

			DrawPreviewTexture();

			if (GUILayout.Button("Save Texture"))
			{
				GenerateAndSaveTexture();
			}
		}
		
		public void OnEnable()
		{
			InitSerializedObjects(); // Can this be done elsewhere?

			GeneratePreviewTexture();
		}
		
		public void OnDestroy()
		{
			if (previewTexture != null)
			{
				DestroyImmediate(previewTexture);
			}
		}
		
		/// <summary>
		/// Draws a header label.
		/// </summary>
		/// <param name="text">The header text.</param>
		// Reuse candidate
		protected static void Header(string text) => EditorGUILayout.LabelField(text, EditorStyles.boldLabel);

		/// <summary>
		/// Draw controls for the specific texture window. 
		/// </summary>
		/// <remarks>
		/// You do not need to draw the image dimensions controls, the preview texture or the save button. 
		/// </remarks>
		protected abstract void DrawPropertiesGui();

		/// <summary>
		/// Use this for initialing serialized objects used to render better UI controls. This is called in
		/// <see cref="OnEnable"/>.
		/// </summary>
		protected abstract void InitSerializedObjects();

		/// <summary>
		/// This function generates the texture based on the current settings.
		/// </summary>
		/// <param name="dimensions"></param>
		protected abstract Texture2D GenerateTexture(Vector2Int dimensions);

		private static void DrawImageSizeControls(ref Vector2Int imageDimensions)
		{
			EditorGUILayout.BeginHorizontal();
			if(GUILayout.Button("512x512"))
			{
				imageDimensions = new Vector2Int(512, 512);
			}
			if(GUILayout.Button("256x256"))
			{
				imageDimensions = new Vector2Int(256, 256);
			}
			if(GUILayout.Button("256x8"))
			{
				imageDimensions = new Vector2Int(256, 8);
			}
				
			EditorGUILayout.EndHorizontal();
			imageDimensions = EditorGUILayout.Vector2IntField("Image Dimensions", imageDimensions);
		}

		private void DrawPreviewTexture()
		{
			if (previewTexture == null)
			{
				return;
			}

			EditorGUILayout.Space();
			Header("Preview");
			var previewRect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(true));
			EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.ScaleToFit);
		}
			
		private void GenerateAndSaveTexture()
		{
			string path = EditorUtility.SaveFilePanel("Save Texture", string.Empty, "GradientTexture.png", "png");

			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			var texture = GenerateTexture(imageDimensions);
			SaveTexture(texture, path);
			DestroyImmediate(texture);

			EditorUtility.DisplayDialog("Success", "Texture saved to:\n" + path, "OK");
		}
		
		private static void SaveTexture(Texture2D texture, string path)
		{
			texture.wrapMode = TextureWrapMode.Clamp;
			byte[] bytes = texture.EncodeToPNG();
			File.WriteAllBytes(path, bytes);
			AssetDatabase.Refresh();
			
			string assetPath = "Assets" + path.Substring(Application.dataPath.Length);
			var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
			
			if (importer != null)
			{
				importer.wrapMode = TextureWrapMode.Clamp;
				importer.SaveAndReimport();
			}
		}

		private void GeneratePreviewTexture()
		{
			if (previewTexture != null)
			{
				DestroyImmediate(previewTexture);
			}

			previewTexture = GenerateTexture(PreviewDimensions);
			Repaint();
		}
	}
	
	/// <summary>
	/// Editor window for generating gradient textures.
	/// </summary>
	public class RampTextureGenerator : TextureGeneratorWindow
	{
		private enum Direction
		{
			X,
			Y,
			Radial,
			Angular
		}

		private enum GradientType
		{
			SingleColor,
			Gradient,
			Curve
		}
		
		private const string ToolName = "Gradient Texture Generator";
		private const string ToolMenuPath = "Gamelogic/Tools/" + ToolName;
		
		private Gradient gradient = new()
		{
			colorKeys = new[]
			{
				new GradientColorKey(Color.black, 0f),
				new GradientColorKey(Color.white, 1f)
			},
			alphaKeys = new[]
			{
				new GradientAlphaKey(1f, 0f),
				new GradientAlphaKey(1f, 1f)
			}
		};

		private AnimationCurve colorCurve = new()
		{
			keys = new[]
			{
				new Keyframe(0, 0)
				{
					outTangent = 1,
				},
				new Keyframe(1, 1)
				{
					inTangent = 1
				}
			},
			
			postWrapMode = WrapMode.Clamp,
			preWrapMode = WrapMode.Clamp,
		};

		private AnimationCurve alphaCurve = new()
		{
			keys = new[]
			{
				new Keyframe(0, 1),
				new Keyframe(1, 1)
			},

			postWrapMode = WrapMode.Clamp,
			preWrapMode = WrapMode.Clamp,
		};
		
		private Color singleColor = Color.white;
		
		private ColorList colorList;
		private SerializedObject serializedObject;
		private SerializedProperty colorsProperty;
		
		private GradientType gradientType = GradientType.Curve;
		private Direction direction = Direction.X;

		private bool discreteSteps = false;
		private int steps = 4;
		private bool flipT = false;
		private float offsetAngle = 0;
		private bool circularGradient;
		
		/// <summary>
		/// Shows an instance of the <see cref="RampTextureGenerator"/> window.
		/// </summary>
		[MenuItem(ToolMenuPath)]
		public static void ShowWindow() => GetWindow<RampTextureGenerator>(ToolName);

		protected override void InitSerializedObjects()
		{
			if (colorList == null)
			{
				colorList = CreateInstance<ColorList>();
			}

			serializedObject = new SerializedObject(colorList);
			colorsProperty = serializedObject.FindRequiredProperty(colorList.ColorsFieldName);
		}

		protected override void DrawPropertiesGui()
		{
			serializedObject.Update();
			gradientType = (GradientType)EditorGUILayout.EnumPopup("Gradient Type", gradientType);
			EditorGUILayout.Space();

			Header("Sample Settings");
			DrawSingleColorControls();
			DrawGradientControls();
			DrawCurveControls();
			DrawDiscreteStepControls();
			EditorGUILayout.Space();

			Header("Generation Settings");
			DrawDirectionControls();
			DrawAngularControls();

			return;

			void DrawSingleColorControls()
			{
				if (gradientType != GradientType.SingleColor)
				{
					return;
				}

				singleColor = EditorGUILayout.ColorField(singleColor);
			}

			void DrawGradientControls()
			{
				if (gradientType != GradientType.Gradient)
				{
					return;
				}

				gradient = EditorGUILayout.GradientField("Gradient", gradient);
			}

			void DrawCurveControls()
			{
				if (gradientType != GradientType.Curve)
				{
					return;
				}

				colorCurve = EditorGUILayout.CurveField("Curve", colorCurve);
				alphaCurve = EditorGUILayout.CurveField("Alpha Curve", alphaCurve);
				EditorGUILayout.PropertyField(colorsProperty, new GUIContent("Colors"), true);
			}

			void DrawDiscreteStepControls()
			{
				EditorGUI.BeginDisabledGroup(gradientType == GradientType.SingleColor);
				discreteSteps = EditorGUILayout.Toggle("Discrete Steps", discreteSteps);
				EditorGUI.BeginDisabledGroup(!discreteSteps);
				steps = EditorGUILayout.IntField("Steps", steps);
				steps = Mathf.Max(1, steps); // Ensure steps is at least 1

				circularGradient = EditorGUILayout.Toggle("Circular gradient", circularGradient);

				EditorGUI.EndDisabledGroup();
				EditorGUI.EndDisabledGroup();
			}

			void DrawDirectionControls()
			{
				EditorGUI.BeginDisabledGroup(gradientType == GradientType.SingleColor);
				direction = (Direction)EditorGUILayout.EnumPopup("Direction", direction);
				flipT = EditorGUILayout.Toggle("Flip T", flipT);
				EditorGUI.EndDisabledGroup();
			}

			void DrawAngularControls()
			{
				if (direction != Direction.Angular)
				{
					return;
				}

				EditorGUI.BeginDisabledGroup(gradientType == GradientType.SingleColor);
				offsetAngle = EditorGUILayout.FloatField("Offset Angle (Degrees)", offsetAngle);
				EditorGUI.EndDisabledGroup();
			}
		}

		protected override Texture2D GenerateTexture(Vector2Int dimensions)
		{
			var texture = new Texture2D(dimensions.x, dimensions.y, TextureFormat.RGBA32, false);

			for (int x = 0; x < dimensions.x; x++)
			{
				for (int y = 0; y < dimensions.y; y++)
				{
					float t = GetT(x, y, dimensions.x, dimensions.y);
					t = GetDiscreteT(t);
					t = GetFlipT(t);
					var color = GetColor(t);
					texture.SetPixel(x, y, color);
				}
			}

			texture.wrapMode = TextureWrapMode.Clamp;
			texture.Apply();
			return texture;
		}

		private float GetFlipT(float t) => flipT ? 1 - t : t;

		private float GetT(int x, int y, int width, int height)
			=> direction switch
			{
				Direction.X => x / (width - 1f),
				Direction.Y => y / (height - 1f),
				Direction.Radial => 2 * Mathf.Sqrt(Sqr(x / (width - 1f) - 0.5f) + Sqr(y / (height - 1f) - 0.5f)),
				Direction.Angular => Mod(Mathf.Atan2(y - height / 2f, x - width / 2f) / (2 * Mathf.PI) + offsetAngle / 360f, 1f),
				_ => throw new ArgumentOutOfRangeException()
			};
		
		private float Mod(float x, float y) => x - y * Mathf.Floor(x / y);

		/// <summary>
		/// Calculates the square of a number.
		/// </summary>
		/// <param name="x">The number to square.</param>
		/// <returns>The square of the number.</returns>
		// Reuse candidate
		private float Sqr(float x) => x * x;

		private float GetDiscreteT(float t)
		{
			if (!discreteSteps)
			{
				return t;
			}

			if (steps == 1)
			{
				return 0;
			}

			float scaled = t * steps;
			float xn = Mathf.FloorToInt(scaled);
			float stepIndex = Mathf.Clamp(xn, 0, steps - 1);
			float stepT = stepIndex / (circularGradient ? steps : steps - 1);
			return stepT;
		}

		private Color GetColor(float t) 
			=> gradientType switch
			{
				GradientType.SingleColor => singleColor,
				GradientType.Gradient => gradient.Evaluate(t),
				GradientType.Curve => colorList.Evaluate(colorCurve.Evaluate(t)),
				_ => throw new ArgumentOutOfRangeException($"No implementation for {gradientType} of type {gradientType.GetType()}")
			};

		
	}
	
	public enum PatternType
	{
		WhiteNoise,
		CheckerBoard,
		HslColor
	}
	
	/// <summary>
	/// Editor window for generating checkerboard textures.
	/// </summary>
	public class PatternTextureGenerator : TextureGeneratorWindow
	{
		private const string ToolName = "Pattern Texture Generator";
		private const string ToolMenuPath = "Gamelogic/Tools/" + ToolName;
		private PatternType patternType = PatternType.CheckerBoard;
		private Vector2Int cellDimensions = new(32, 32);
		private Color color1 = Color.black;
		private Color color2 = Color.white;
		private Texture2D previewTexture;

		[MenuItem(ToolMenuPath)]
		public static void ShowWindow() => GetWindow<PatternTextureGenerator>(ToolName);

		protected override void InitSerializedObjects()
		{
			// Nothing to do. 
		}

		protected override void DrawPropertiesGui()
		{
			Header("Colors");
			color1 = EditorGUILayout.ColorField("Color 1", color1);
			color2 = EditorGUILayout.ColorField("Color 2", color2);
			
			patternType = (PatternType)EditorGUILayout.EnumPopup("Pattern Type", patternType);
			
			Header("Pattern Settings");

			cellDimensions = patternType switch
			{
				PatternType.CheckerBoard => EditorGUILayout.Vector2IntField("Cell Dimensions", cellDimensions),
				_ => cellDimensions
			};

			Header("Output Settings");
		}

		protected override Texture2D GenerateTexture(Vector2Int dimensions)
		{
			switch (patternType)
			{
				case PatternType.CheckerBoard:
					return GenerateTexture_CheckerBoard(dimensions);
				case PatternType.WhiteNoise:
					return GenerateTexture_WhiteNoise(dimensions);
				case PatternType.HslColor:
					return GenerateTexture_HslColor(dimensions);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private Texture2D GenerateTexture_CheckerBoard(Vector2Int dimensions)
		{
			var texture = new Texture2D(dimensions.x, dimensions.y, TextureFormat.RGBA32, false);

			for (int x = 0; x < dimensions.x; x++)
			{
				for (int y = 0; y < dimensions.y; y++)
				{
					bool isColor1 = ((x / cellDimensions.x) + (y / cellDimensions.y)) % 2 == 0;
					texture.SetPixel(x, y, isColor1 ? color1 : color2);
				}
			}

			texture.wrapMode = TextureWrapMode.Clamp;
			texture.Apply();
			return texture;
		}
		
		private Texture2D GenerateTexture_WhiteNoise(Vector2Int dimensions)
		{
			var texture = new Texture2D(dimensions.x, dimensions.y, TextureFormat.RGBA32, false);

			for (int x = 0; x < dimensions.x; x++)
			{
				for (int y = 0; y < dimensions.y; y++)
				{
					float randomValue = Random.value; // Random value between 0 and 1
					Color randomColor = Color.Lerp(color1, color2, randomValue);
					texture.SetPixel(x, y, randomColor);
				}
			}

			texture.wrapMode = TextureWrapMode.Clamp;
			texture.Apply();
			return texture;
		}
		
		private Texture2D GenerateTexture_HslColor(Vector2Int dimensions)
		{
			var texture = new Texture2D(dimensions.x, dimensions.y, TextureFormat.RGBA32, false);

			for (int x = 0; x < dimensions.x; x++)
			{
				for (int y = 0; y < dimensions.y; y++)
				{
					float u = x / (dimensions.x - 1f);
					float v = y / (dimensions.y - 1f);
					var hsl = Color.HSVToRGB(u, 1f, 1f);
					
					var color = v < 0.5 
						? Color.Lerp(Color.black, hsl, v * 2f) 
						: Color.Lerp(hsl, Color.white, (v - 0.5f) * 2f);
						
					texture.SetPixel(x, y, color);
				}
			}

			texture.wrapMode = TextureWrapMode.Clamp;
			texture.Apply();
			return texture;
		}
	}
}
