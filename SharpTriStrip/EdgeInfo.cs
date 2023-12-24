namespace SharpTriStrip
{
	/// <summary>
	/// Represents a single edge that one or two <see cref="FaceInfo"/> share. Used internally only.
	/// </summary>
	public class EdgeInfo
	{
		/// <summary>
		/// Number of objects that reference this <see cref="EdgeInfo"/>.
		/// </summary>
		public int RefCount { get; set; }

		/// <summary>
		/// First <see cref="FaceInfo"/> that contains this <see cref="EdgeInfo"/>.
		/// </summary>
		public FaceInfo Face0 { get; set; }

		/// <summary>
		/// Second <see cref="FaceInfo"/> that contains this <see cref="EdgeInfo"/>.
		/// </summary>
		public FaceInfo Face1 { get; set; }

		/// <summary>
		/// Start index of this <see cref="EdgeInfo"/>.
		/// </summary>
		public int V0 { get; set; }

		/// <summary>
		/// End index of this <see cref="EdgeInfo"/>.
		/// </summary>
		public int V1 { get; set; }

		/// <summary>
		/// First <see cref="EdgeInfo"/> that this <see cref="EdgeInfo"/> points to.
		/// </summary>
		public EdgeInfo Next0 { get; set; }

		/// <summary>
		/// Second <see cref="EdgeInfo"/> that this <see cref="EdgeInfo"/> points to.
		/// </summary>
		public EdgeInfo Next1 { get; set; }

		/// <summary>
		/// Creates a new instance of <see cref="EdgeInfo"/> with two indices provided.
		/// </summary>
		/// <param name="v0">Start index of this <see cref="EdgeInfo"/>.</param>
		/// <param name="v1">End index of this <see cref="EdgeInfo"/>.</param>
		public EdgeInfo(int v0, int v1)
		{
			this.V0 = v0;
			this.V1 = v1;
			this.RefCount = 2;
		}
	}
}
