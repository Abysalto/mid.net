using System.Text;
using AbySalto.Mid.Application.Interfaces.Networking;
using AbySalto.Mid.Domain.Abstraction.Conversion;
using AbySalto.Mid.Domain.Abstraction.Networking;
using AbySalto.Mid.Domain.Business.Conversion;
using AbySalto.Mid.Domain.Business.Networking;
using AbySalto.Mid.Domain.Data.Deserialized;
using AbySalto.Mid.Domain.Data.DTO;
using AbySalto.Mid.Domain.Data.Model;
using Newtonsoft.Json;

namespace AbySalto.Mid.Application.Products.Networking
{
    public class GetFilteredProductsClient : IClient<ParamsModel, ProductDto[]>
    {
        private readonly IMapper<ProductDeserialized, ProductDto> mapper;
        private readonly UriFactory _uriFactory;
        private readonly string rootPage = "dummyjson.com";

        public GetFilteredProductsClient()
        {
            _uriFactory = new UriFactory(rootPage);
            mapper = new DeserializedToDtoMapper();
        }

        public async Task<ProductDto[]> SendWithResult(ParamsModel model)
        {

            string uri = _uriFactory.GetUriForPaginationAndSorting(model.Page, model.SortElements ?? "title", model.OrderBy);

            using (HttpClient client = new HttpClient())
            {
                var result = await client.GetAsync(uri);
                Stream bytes = result.Content.ReadAsStream();

                byte[] buffer = new byte[bytes.Length];
                bytes.ReadExactly(buffer);
                string json = Encoding.UTF8.GetString(buffer);

                List<ProductDeserialized> items = JsonConvert.DeserializeObject<Root>(json)?.products ?? throw new Exception("Json unable to deserialize due to validity.");
                ProductDto[] dtos = items.Select(mapper.Map).ToArray();

                return dtos;

            }
        }
    }
}
