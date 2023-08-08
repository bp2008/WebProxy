namespace WebProxy
{
	/// <summary>
	/// Maps an entrypoint to an exitpoint.  Entrypoints and exitpoints have an N-to-N relationship (there is no limit to the number of routes each entrypoint or exitpoint can exist in).
	/// </summary>
	public class ProxyRoute
	{
		/// <summary>
		/// Name of the entrypoint which is mapped to an exitpoint by this route.
		/// </summary>
		public string entrypointName;
		/// <summary>
		/// Name of the exitpoint which is mapped to an entrypoint by this route.
		/// </summary>
		public string exitpointName;
		/// <summary>
		/// Returns a string concisely describing the route.
		/// </summary>
		/// <returns>A concise description of the route.</returns>
		public override string ToString()
		{
			return entrypointName + " -> " + exitpointName;
		}
	}
}