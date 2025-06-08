# UNChat - Internetowy Komunikator

UNChat to nowoczesny komunikator internetowy stworzony z wykorzystaniem technologii ASP.NET Core MVC, JavaScript oraz SignalR, umożliwiający komunikację w czasie rzeczywistym. Aplikacja oferuje kompleksowy zestaw funkcjonalności, takich jak czaty prywatne i grupowe, obsługa multimediów, powiadomienia push oraz integrację z zewnętrznymi usługami.

##  Funkcjonalności

- **Komunikacja w czasie rzeczywistym** dzięki SignalR.
- **Logowanie i rejestracja** z możliwością autoryzacji przez Google.
- **Czaty prywatne i grupowe** z obsługą emoji, GIF-ów, wiadomości głosowych, plików oraz reakcji.
- **Powiadomienia push** oraz wewnętrzne powiadomienia o nowych wiadomościach.
- **Historia wiadomości** z wyszukiwaniem i możliwością edycji/usuwania.
- **Zarządzanie kontaktami** oraz czatami grupowymi.
- **Tryb ciemny/jasny** zapamiętywany przez aplikacje.
- **Generowanie dokumentów PDF** (QuestPDF).

##  Wykorzystane technologie

- **Backend**: ASP.NET Core MVC, SignalR, Entity Framework Core, ASP.NET Core Identity.
- **Frontend**: JavaScript, HTML, CSS.
- **Baza danych**: SQL Server.
- **Integracje**: Google Authentication (OAuth), Web Push.
- **Narzędzia**: QuestPDF (generowanie PDF), Swagger (dokumentacja API).

##  Instalacja

1. **Wymagania wstępne**:
   - .NET 8.0
   - Visual Studio 2022
   - SQL Server

2. **Kroki instalacji**:
   - Sklonuj repozytorium.
   - Otwórz plik `UNChat.sln` w Visual Studio 2022.
   - W `SQL Server Object Explorer` utwórz nową bazę danych o nazwie `UNChatDb`.
   - Uruchom aplikację poprzez naciśnięcie przycisku start w Visual Studio.

3. **Dokumentacja API**:
   - Dostępna pod adresem: `https://localhost:nr_portu/swagger`.


##  Autorzy

- Tomasz Kuczyński 
- Agata Sawicka 
- Michał Targoński 

