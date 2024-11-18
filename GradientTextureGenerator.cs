// Author: Herman Tulleken
// gamelogic.co.za

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

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
	/// Editor window for generating gradient textures.
	/// </summary>
	public class GradientTextureGenerator : EditorWindow
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
			Gradient,
			Curve
		}
		
		private const string ToolName = "Gradient Texture Generator";
		private const string ToolMenuPath = "Gamelogic/Tools/" + ToolName;
		private const int DefaultImageSize = 256;
		private static readonly Vector2Int PreviewImageSizeMax = new(256, 256);
		
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

		private AnimationCurve colorCurve = new();
		private AnimationCurve alphaCurve = new();
		
		private ColorList colorList;
		private SerializedObject serializedObject;
		private SerializedProperty colorsProperty;
		private Vector2Int imageDimensions = new(DefaultImageSize, DefaultImageSize);

		private GradientType gradientType = GradientType.Gradient;
		private Direction direction = Direction.X;

		private bool discreteSteps = false;
		private int steps = 4;
		private bool flipT = false;
		private float offsetAngle = 0;
		private bool circularGradient;

		private Texture2D previewTexture;


		[MenuItem(ToolMenuPath)]
		public static void ShowWindow() => GetWindow<GradientTextureGenerator>(ToolName);

		private void OnEnable()
		{
			if (colorList == null)
			{
				colorList = CreateInstance<ColorList>();
			}

			serializedObject = new SerializedObject(colorList);
			colorsProperty = serializedObject.FindRequiredProperty(colorList.ColorsFieldName);
			var previewDimensions = Vector2Int.Min(PreviewImageSizeMax, imageDimensions);
			GeneratePreviewTexture(previewDimensions);
		}

		private void OnGUI()
		{
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

			gradientType = (GradientType)EditorGUILayout.EnumPopup("Gradient Type", gradientType);
			EditorGUILayout.Space();
			
			Header("Sample Settings");
			DrawGradientControls();
			DrawCurveControls();
			DrawDiscreteStepControls();
			EditorGUILayout.Space();
			
			Header("Generation Settings");
			DrawDirectionControls();
			DrawAngularControls();
			DrawImageSizeControls();

			var previewDimensions = Vector2Int.Min(PreviewImageSizeMax, imageDimensions);

			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
				GeneratePreviewTexture(previewDimensions);
			}

			DrawPreviewTexture();
			EditorGUILayout.Space();
			
			if (GUILayout.Button("Save Texture"))
			{
				GenerateAndSaveTexture();
			}

			return;
			
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
				discreteSteps = EditorGUILayout.Toggle("Discrete Steps", discreteSteps);
				EditorGUI.BeginDisabledGroup(!discreteSteps);
				steps = EditorGUILayout.IntField("Steps", steps);
				steps = Mathf.Max(1, steps); // Ensure steps is at least 1
				
				circularGradient = EditorGUILayout.Toggle("Circular gradient", circularGradient);
				
				EditorGUI.EndDisabledGroup();
			}

			void DrawImageSizeControls() 
				=> imageDimensions = EditorGUILayout.Vector2IntField("Image Dimensions", imageDimensions);

			void DrawDirectionControls()
			{
				direction = (Direction)EditorGUILayout.EnumPopup("Direction", direction);
				flipT = EditorGUILayout.Toggle("Flip T", flipT);
			}
			
			void DrawAngularControls()
			{
				if (direction != Direction.Angular)
				{
					return;
				}
				
				offsetAngle = EditorGUILayout.FloatField("Offset Angle (Degrees)", offsetAngle);
			}

			void DrawPreviewTexture()
			{
				if (previewTexture == null)
				{
					return;
				}
				
				EditorGUILayout.Space();
				Header("Preview");
				var previewRect = GUILayoutUtility.GetRect(previewDimensions.x, previewDimensions.y, GUILayout.ExpandWidth(true));
				EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.ScaleToFit);
			}

			
		}

		/// <summary>
		/// Draws a header label.
		/// </summary>
		/// <param name="text">The header text.</param>
		// Reuse candidate
		private void Header(string text) => EditorGUILayout.LabelField(text, EditorStyles.boldLabel);

		private void GeneratePreviewTexture(Vector2Int previewDimensions)
		{
			if (previewTexture != null)
			{
				DestroyImmediate(previewTexture);
			}

			previewTexture = GenerateTexture(previewDimensions);
			Repaint();
		}

		private void GenerateAndSaveTexture()
		{
			string path = EditorUtility.SaveFilePanel("Save Texture", string.Empty, "GradientTexture.png", "png");

			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			var texture = GenerateTexture(imageDimensions);
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

			
			DestroyImmediate(texture);

			EditorUtility.DisplayDialog("Success", "Texture saved to:\n" + path, "OK");
		}

		private Texture2D GenerateTexture(Vector2Int dimensions)
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
			float stepT = stepIndex / (circularGradient ? steps : (steps - 1));
			return stepT;
		}

		private Color GetColor(float t) 
			=> gradientType switch
			{
				GradientType.Gradient => gradient.Evaluate(t),
				GradientType.Curve => colorList.Evaluate(colorCurve.Evaluate(t)),
				_ => throw new ArgumentOutOfRangeException($"No implementation for {gradientType} of type {gradientType.GetType()}")
			};

		private void OnDestroy()
		{
			if (previewTexture != null)
			{
				DestroyImmediate(previewTexture);
			}
		}
	}
}
