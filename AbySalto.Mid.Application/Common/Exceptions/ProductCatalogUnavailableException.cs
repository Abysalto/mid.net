namespace AbySalto.Mid.Application.Common.Exceptions;

public class ProductCatalogUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
