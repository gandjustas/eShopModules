namespace eShop.Mobile;

// Адрес хоста WebApp для мобильных клиентов: Identity, каталог, заказы и корзина - модули одного процесса.
// Файл подключён ссылкой в ClientApp, HybridApp и AppHost: AppHost открывает у WebApp эти порты отдельно от
// профилей launchSettings, поэтому клиент не зависит от них.
// Android-эмулятор тоже ходит на localhost: перед запуском выполните
//   adb reverse tcp:17298 tcp:17298 && adb reverse tcp:15045 tcp:15045
// Адрес 10.0.2.2 не нужен.
internal static class WebAppAddress
{
    public const int HttpsPort = 17298;
    public const int HttpPort = 15045;

    // gRPC корзины и вход идут по https: на открытом http2 хост не слушает
    public const string Https = "https://localhost:17298";

    public const string Http = "http://localhost:15045";
}
