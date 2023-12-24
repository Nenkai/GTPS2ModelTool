using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SharpTriStrip
{
	/// <summary>
	/// Provides methods for generating triangle strips and optimizing index buffers.
	/// </summary>
	public class TriStrip
	{
		/// <summary>
		/// Type of <see cref="PrimitiveGroup"/> that are being generated when calling 
		/// <see cref="GenerateStrips(ushort[], out PrimitiveGroup[], bool)"/>.
		/// </summary>
		public enum PrimitiveType : int
		{
			/// <summary>
			/// <see cref="PrimitiveGroup"/> that contains optimized index buffers and/or separated faces/triangles.
			/// </summary>
			List,

			/// <summary>
			/// <see cref="PrimitiveGroup"/> that contains faces/triangles organized in a single strip.
			/// </summary>
			Strip,
		}

		/// <summary>
		/// A wrapper around index buffer that specifies its type.
		/// </summary>
		public class PrimitiveGroup
		{
			/// <summary>
			/// <see cref="PrimitiveType"/> of this <see cref="PrimitiveGroup"/>.
			/// </summary>
			public PrimitiveType Type { get; }

			/// <summary>
			/// Index buffer array that this <see cref="PrimitiveGroup"/> wraps.
			/// </summary>
			public ushort[] Indices { get; }
			
			/// <summary>
			/// Creates a new instance of <see cref="PrimitiveGroup"/> with zero-length index buffer.
			/// </summary>
			public PrimitiveGroup()
			{
				this.Type = PrimitiveType.Strip;
				this.Indices = Array.Empty<ushort>();
			}

			/// <summary>
			/// Creates a new instance of <see cref="PrimitiveGroup"/> with type specified and index buffer of length given.
			/// </summary>
			/// <param name="type"><see cref="PrimitiveType"/> of this group.</param>
			/// <param name="numIndices">Length of index buffer of this group.</param>
			public PrimitiveGroup(PrimitiveType type, int numIndices)
			{
				this.Type = type;
				this.Indices = new ushort[numIndices];
			}

			/// <summary>
			/// Creates a new instance of <see cref="PrimitiveGroup"/> with type specified and index buffer given.
			/// </summary>
			/// <param name="type"><see cref="PrimitiveType"/> of this group.</param>
			/// <param name="indices">Index buffer of this group. If buffer is <see langword="null"/>, it is being
			/// initialized to an array of zero length.</param>
			public PrimitiveGroup(PrimitiveType type, ushort[] indices)
			{
				this.Type = type;
				this.Indices = indices ?? Array.Empty<ushort>();
			}
		};

		private const int CACHESIZE_GEFORCE1_2 = 16;
		private const int CACHESIZE_GEFORCE3 = 24;

		private int m_cacheSize = CACHESIZE_GEFORCE1_2;
		private bool m_stitchStrips = true;
		private int m_minStripSize = 0;
		private bool m_listsOnly = false;
		private int m_restartValue = 0;
		private bool m_restart = false;
		
		/// <summary>
		/// Creates a new instance of <see cref="TriStrip"/>.
		/// </summary>
		public TriStrip()
		{
		}

		private static bool SameTriangle(int v00, int v01, int v02, int v10, int v11, int v12)
		{
			bool isSame = false;

			if (v00 == v10)
			{
				if (v01 == v11)
				{
					if (v02 == v12)
					{
						isSame = true;
					}
				}
			}
			else if (v00 == v11)
			{
				if (v01 == v12)
				{
					if (v02 == v10)
					{
						isSame = true;
					}
				}
			}
			else if (v00 == v12)
			{
				if (v01 == v10)
				{
					if (v02 == v11)
					{
						isSame = true;
					}
				}
			}

			return isSame;
		}

		private static bool TestTriangle(ushort v0, ushort v1, ushort v2, List<FaceInfo>[] bins, int binModule)
		{
			// hash index zero of this triangle
			int ctr = v0 & binModule;

			// try find corresponding triangle
			for (int k = 0; k < bins[ctr].Count; ++k)
			{
				// check triangles in this bin
				var test = bins[ctr][k];

				if (TriStrip.SameTriangle(test.V0, test.V1, test.V2, v0, v1, v2))
				{
					return true;
				}
			}

			// hash index one of this triangle
			ctr = v1 & binModule;

			// try find corresponding triangle
			for (int k = 0; k < bins[ctr].Count; ++k)
			{
				// check triangles in this bin
				var test = bins[ctr][k];

				if (TriStrip.SameTriangle(test.V0, test.V1, test.V2, v0, v1, v2))
				{
					return true;
				}
			}

			// hash index two of this triangle
			ctr = v2 & binModule;

			// try find corresponding triangle
			for (int k = 0; k < bins[ctr].Count; ++k)
			{
				// check triangles in this bin
				var test = bins[ctr][k];

				if (TriStrip.SameTriangle(test.V0, test.V1, test.V2, v0, v1, v2))
				{
					return true;
				}
			}

			// return false if nothing found
			return false;
		}

		/// <summary>
		/// For GPUs that support primitive restart, this sets a value as the restart index.
		/// Restart is meaningless if strips are not being stitched together, so enabling restart
		/// makes <see cref="TriStrip"/> forcing stitching. So, you'll get back one strip.
		/// </summary>
		/// <param name="restartValue">Restart value to set if restart should be enabled.</param>
		public void EnableRestart(int restartValue)
		{
			this.m_restart = true;
			this.m_restartValue = restartValue;
		}

		/// <summary>
		/// For GPUs that support primitive restart, this disables using primitive restart.
		/// </summary>
		public void DisableRestart()
		{
			this.m_restart = false;
		}

		/// <summary>
		/// Sets the cache size which the stripifier uses to optimize the data.
		/// Controls the length of the generated individual strips.
		/// This is the "actual" cache size, so 24 for GeForce3 and 16 for GeForce1/2
		/// You may want to play around with this number to tweak performance.
		/// </summary>
		/// <param name="cacheSize">Cache size to set. Default value is 16.</param>
		public void SetCacheSize(int cacheSize = 16)
		{
			this.m_cacheSize = cacheSize;
		}

		/// <summary>
		/// Value to indicate whether to stitch together strips into one huge strip or not.
		/// If set to <see langword="true"/>, you'll get back one huge strip stitched together using degenerate
		/// triangles. If set to <see langword="false"/>, you'll get back a large number of separate strips.
		/// </summary>
		/// <param name="stitchStrips"><see langword="true"/> to stitch strips; otherwise, <see langword="false"/>.
		/// Default value is <see langword="true"/>.</param>
		public void SetStitchStrips(bool stitchStrips = true)
		{
			this.m_stitchStrips = stitchStrips;
		}

		/// <summary>
		/// Sets the minimum acceptable size for a strip, in triangles.
		/// All strips generated which are shorter than this will be thrown into one big, separate list.
		/// </summary>
		/// <param name="minSize">Minimum size of a single strip. Default value is 0.</param>
		public void SetMinStripSize(int minSize = 0)
		{
			this.m_minStripSize = minSize;
		}

		/// <summary>
		/// If set to <see langword="true"/>, will return an optimized list, with no strips at all.
		/// </summary>
		/// <param name="listsOnly"><see langword="true"/> if return lists only; otherwise, <see langword="false"/>.
		/// Default value is <see langword="false"/>.</param>
		public void SetListsOnly(bool listsOnly = false)
		{
			this.m_listsOnly = listsOnly;
		}

		/// <summary>
		/// Generates triangle strips based on the index buffer provided.
		/// </summary>
		/// <param name="indices">Input index buffer to generate strips.</param>
		/// <param name="primGroups"><see cref="Array"/> of optimized/stripified <see cref="PrimitiveGroup"/>.</param>
		/// <param name="validateEnabled"><see langword="true"/> if enable index and strip validation;
		/// otherwise, <see langword="false"/>.</param>
		/// <returns><see langword="true"/> if triangles strips have been successfully generated (and, optionally,
		/// verified); otherwise, <see langword="false"/>.</returns>
		public bool GenerateStrips(ushort[] indices, out PrimitiveGroup[] primGroups, bool validateEnabled = false)
		{
			if (indices is null || indices.Length == 0)
			{
				primGroups = null;
				return false;
			}

			//put data in format that the stripifier likes
			var tempIndices = new List<ushort>(indices);

			ushort maxIndex = indices[0];
			ushort minIndex = maxIndex;
			
			for (int i = 0; i < indices.Length; i++)
			{
				var index = indices[i];

				if (index > maxIndex)
				{
					maxIndex = index;
				}
				else if (index < minIndex)
				{
					minIndex = index;
				}
			}

			var stripifier = new Stripifier();

			// do actual stripification
			stripifier.Stripify(tempIndices, this.m_cacheSize, this.m_minStripSize, maxIndex, out var tempStrips, out var tempFaces);

			if (this.m_listsOnly)
			{
				// if we're outputting only lists, we're done
				primGroups = new PrimitiveGroup[1];

				// count the total number of indices
				int numIndices = 0;

				// every face is 3 indices
				for (int i = 0; i < tempStrips.Count; ++i)
				{
					numIndices += tempStrips[i].FaceInfos.Count * 3;
				}

				// add in the list
				numIndices += tempFaces.Count * 3;

				// make shared primitive group
				var primIndices = new ushort[numIndices];

				// init counter
				int indexCtr = 0;
				var needsResize = false;

				// do strips
				for (int i = 0; i < tempStrips.Count; ++i)
				{
					var tempStrip = tempStrips[i];

					for (int j = 0; j < tempStrip.FaceInfos.Count; ++j)
					{
						var faceInfo = tempStrip.FaceInfos[j];

						// degenerates are of no use with lists
						if (!Stripifier.IsDegenerate(faceInfo))
						{
							primIndices[indexCtr++] = (ushort)faceInfo.V0;
							primIndices[indexCtr++] = (ushort)faceInfo.V1;
							primIndices[indexCtr++] = (ushort)faceInfo.V2;
						}
						else
						{
							// remove degenerate and enable resize array
							numIndices -= 3;
							needsResize = true;
						}
					}
				}

				// do lists
				for (int i = 0; i < tempFaces.Count; ++i)
				{
					var faceInfo = tempFaces[i];

					primIndices[indexCtr++] = (ushort)faceInfo.V0;
					primIndices[indexCtr++] = (ushort)faceInfo.V1;
					primIndices[indexCtr++] = (ushort)faceInfo.V2;
				}

				// resize according to correct size
				if (needsResize)
				{
					Array.Resize(ref primIndices, numIndices);
				}

				// finally, put into the final array
				primGroups[0] = new PrimitiveGroup(PrimitiveType.List, primIndices);
			}
			else
			{
				stripifier.CreateStrips(tempStrips, this.m_stitchStrips, this.m_restart, this.m_restartValue, out var stripIndices, out int numStrips);

				// if we're stitching strips together, we better get back only one strip from CreateStrips()
				Debug.Assert((this.m_stitchStrips && numStrips == 1) || !this.m_stitchStrips);

				int numGroups = numStrips + ((tempFaces.Count == 0) ? 0 : 1);

				primGroups = new PrimitiveGroup[numGroups];

				// first, the strips				
				for (int stripCtr = 0, startingLoc = 0; stripCtr < numStrips; ++stripCtr)
				{
					int stripLength;

					if (!this.m_stitchStrips)
					{
						// if we've got multiple strips, we need to figure out the correct length
						int i;

						for (i = startingLoc; i < stripIndices.Count; ++i)
						{
							if (stripIndices[i] == -1)
							{
								break;
							}
						}

						stripLength = i - startingLoc;
					}
					else
					{
						stripLength = stripIndices.Count;
					}

					var primGroup = new PrimitiveGroup(PrimitiveType.Strip, stripLength);

					for (int i = startingLoc, indexCtr = 0; i < stripLength + startingLoc; ++i, ++indexCtr)
					{
						primGroup.Indices[indexCtr] = (ushort)stripIndices[i];
					}

					// we add 1 to account for the -1 separating strips
					// this doesn't break the stitched case since we'll exit the loop
					startingLoc += stripLength + 1;

					// finally, put the primitive group into final array
					primGroups[stripCtr] = primGroup;
				}

				// final, the list
				if (numGroups > numStrips)
				{
					var primGroup = new PrimitiveGroup(PrimitiveType.List, tempFaces.Count * 3);
					
					for (int i = 0, indexCtr = 0; i < tempFaces.Count; ++i)
					{
						var tempFace = tempFaces[i];

						primGroup.Indices[indexCtr++] = (ushort)tempFace.V0;
						primGroup.Indices[indexCtr++] = (ushort)tempFace.V1;
						primGroup.Indices[indexCtr++] = (ushort)tempFace.V2;
					}

					primGroups[numStrips] = primGroup;
				}
			}

			// validate generated data against input
			if (validateEnabled)
			{
				const int kBinModule = 0x7F;

				var bins = new List<FaceInfo>[kBinModule + 1];

				for (int i = 0; i < bins.Length; ++i)
				{
					bins[i] = new List<FaceInfo>();
				}

				// hash input indices on first index
				for (int i = 0; i < indices.Length; i += 3)
				{
					var faceInfo = new FaceInfo(indices[i], indices[i + 1], indices[i + 2]);
					bins[indices[i] & kBinModule].Add(faceInfo);
				}

				for (int i = 0; i < primGroups.Length; ++i)
				{
					var primGroup = primGroups[i];
					var primIndices = primGroup.Indices;

					switch (primGroup.Type)
					{
						case PrimitiveType.List:
							{
								for (int j = 0; j < primIndices.Length; j += 3)
								{
									var v0 = primIndices[j];
									var v1 = primIndices[j + 1];
									var v2 = primIndices[j + 2];

									// ignore degenerates
									if (Stripifier.IsDegenerate(v0, v1, v2))
									{
										continue;
									}

									if (!TriStrip.TestTriangle(v0, v1, v2, bins, kBinModule))
									{
										return false; // gigantic fail
									}
								}

								break;
							}

						case PrimitiveType.Strip:
							{
								bool flip = false;

								for (int j = 2; j < primIndices.Length; ++j)
								{
									var v0 = primIndices[j - 2];
									var v1 = primIndices[j - 1];
									var v2 = primIndices[j];

									if (flip)
									{
										// swap v1 and v2
										var temp = v1;
										v1 = v2;
										v2 = temp;
									}

									// ignore degenerates
									if (Stripifier.IsDegenerate(v0, v1, v2))
									{
										flip = !flip;
										continue;
									}

									if (!TriStrip.TestTriangle(v0, v1, v2, bins, kBinModule))
									{
										return false; // you know what, I give up, goodbye!
									}

									flip = !flip;
								}

								break;
							}

						default:
							break;
					}
				}
			}

			return true;
		}

		/// <summary>
		/// Method to remap your indices to improve spatial locality in your vertex buffer.
		/// </summary>
		/// <param name="primGroups"><see cref="Array"/> of <see cref="PrimitiveGroup"/> you want remapped.</param>
		/// <param name="numVerts">Number of vertices in your vertex buffer, also can be thought of as the range
		/// of acceptable values for indices in your primitive groups.</param>
		/// <returns><see cref="Array"/> of remapped <see cref="PrimitiveGroup"/>. Note that vertex buffers
		/// have to be remapped accordingly.</returns>
		public PrimitiveGroup[] RemapIndices(PrimitiveGroup[] primGroups, ushort numVerts)
		{
			var remappedGroups = new PrimitiveGroup[primGroups.Length];

			// caches oldIndex --> newIndex conversion
			var indexCache = new int[numVerts];
			Utils.Memset(indexCache, Byte.MaxValue, numVerts);

			// loop over primitive groups
			for (int i = 0, indexCtr = 0; i < remappedGroups.Length; i++)
			{
				var primGroup = primGroups[i];
				int numIndices = primGroup.Indices.Length;
				var remappedGroup = new PrimitiveGroup(primGroup.Type, numIndices);

				for (int j = 0; j < numIndices; j++)
				{
					int cachedIndex = indexCache[primGroup.Indices[j]];

					if (cachedIndex == -1) // we haven't seen this index before
					{
						// point to "last" vertex in VB
						remappedGroup.Indices[j] = (ushort)indexCtr;

						// add to index cache, increment
						indexCache[primGroup.Indices[j]] = indexCtr++;
					}
					else
					{
						// we've seen this index before
						remappedGroup.Indices[j] = (ushort)cachedIndex;
					}
				}
			}

			return remappedGroups;
		}
	}
}
