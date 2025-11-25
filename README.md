# UNChat - Internetowy Komunikator

UNChat to nowoczesny komunikator internetowy stworzony z wykorzystaniem technologii ASP.NET Core MVC, JavaScript oraz SignalR, umożliwiający komunikację w czasie rzeczywistym. Aplikacja oferuje kompleksowy zestaw funkcjonalności, takich jak czaty prywatne i grupowe, obsługa multimediów, powiadomienia push oraz integrację z zewnętrznymi usługami.

<img width="1223" height="779" alt="image" src="https://github.com/user-attachments/assets/7680f2ba-c771-47d6-ad87-80c37a78dbca" />
Możliwość komunikacji pomiędzy indywidualnymi użytkownikami czat odświeża się "na bieżąco" otrzymujemy wiadomość od razu po wysłaniu.

<img width="521" height="129" alt="image" src="https://github.com/user-attachments/assets/96142b11-004d-43f5-b00c-fbb40f434af1" />

Możliwość zapraszania do znajomych. Status online/offline (wraz z informacją o czasie od ostatniej aktywności)

<img width="379" height="327" alt="image" src="https://github.com/user-attachments/assets/d3c6fa49-4866-4c01-b33b-4504ab5cc5ed" />

Możliwość przesyłania lokalizacji innym użytkownikom. Na każdą wiadomość można reagować emotikonami.

<img width="349" height="112" alt="image" src="https://github.com/user-attachments/assets/4ffa556a-7e85-4793-b5c0-bd7978feab41" />

System powiadomień wewnątrz aplikacji oraz jako powiadomienia push systemowe.
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
- **Narzędzia**: Swagger (dokumentacja API).

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

