using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace EXDWorker;

public class ThaliakClient
{
	public static async Task<GameVersion> GetLatestVersion()
	{
		var gql = new GraphQLHttpClient(@"https://thaliak.xiv.dev/graphql/", new SystemTextJsonSerializer());
		var req = new GraphQLRequest
		{
			Query = """
			{
				game: repository(slug:"4e9a232b") {
					latestVersion {
						versionString
					}
				}
			}
			""",
		};
		var resp = await gql.SendQueryAsync<Data>(req);
		return GameVersion.Parse(resp.Data.Game.LatestVersion.VersionString);
	}
	
	public static async Task<List<string>> GetPatchUrls()
	{
		var gql = new GraphQLHttpClient(@"https://thaliak.xiv.dev/graphql/", new SystemTextJsonSerializer());
		var req = new GraphQLRequest
		{
			Query = """
			{
			  game: repository(slug:"4e9a232b") {
			    versions {
			      patches {
			        url
			      }
			    }
			  }
			}
			""",
		};
		var resp = await gql.SendQueryAsync<Data>(req);
		return resp.Data.Game.Versions.SelectMany(v => v.Patches).Select(v => v.Url).ToList();
	}

	public class Data
	{
		public Game Game { get; set; }
	}

	public class Game
	{
		public LatestVersion LatestVersion { get; set; }
		public List<Version> Versions { get; set; }
	}
	
	public class LatestVersion
	{
		public string VersionString { get; set; }
	}

	public class Version
	{
		public List<Patch> Patches { get; set; }
	}

	public class Patch
	{
		public string Url { get; set; }
	}

	public static void TestThing()
	{
		Console.WriteLine(GetLatestVersion().Result);
		GetPatchUrls().Result.ForEach(Console.WriteLine);
	}
}