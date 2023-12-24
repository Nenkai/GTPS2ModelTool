namespace SharpTriStrip
{
	/// <summary>
	/// Provides general information about start of a strip. Used internally only.
	/// </summary>
	public class StripStartInfo
	{
		/// <summary>
		/// First <see cref="FaceInfo"/> of the strip.
		/// </summary>
		public FaceInfo Face { get; }
		
		/// <summary>
		/// First <see cref="EdgeInfo"/> of the strip.
		/// </summary>
		public EdgeInfo Edge { get; }

		/// <summary>
		/// Controls clockwise orientation of the faces in the strip.
		/// </summary>
		public bool ToV1 { get; }

		/// <summary>
		/// Creates new instance of <see cref="StripStartInfo"/> with values provided.
		/// </summary>
		/// <param name="face">First <see cref="FaceInfo"/> of the strip.</param>
		/// <param name="edge">First <see cref="EdgeInfo"/> of the strip.</param>
		/// <param name="toV1">Controls clockwise orientation of the faces in the strip.</param>
		public StripStartInfo(FaceInfo face, EdgeInfo edge, bool toV1)
		{
			this.Face = face;
			this.Edge = edge;
			this.ToV1 = toV1;
		}
	}
}
