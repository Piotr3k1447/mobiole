# Barbara Kierowca (.NET MAUI)

Projekt przenosi funkcje aplikacji kierowcy do .NET MAUI. Korzysta z istniejącego
`driver-api.php` i wymaga serwera HTTPS. Domyślny adres logowania to
`https://beta.barbara.net.pl`; można go zmienić na ekranie logowania.

## Kompilacja

W Visual Studio otwórz plik rozwiązania i wybierz platformę Android, iOS lub
Windows. Z wiersza poleceń:

```powershell
dotnet build "Barbara Kierowca.csproj" -f net10.0-android
dotnet build "Barbara Kierowca.csproj" -f net10.0-windows10.0.19041.0
```

Kompilacja i podpisanie instalatora iOS wymagają komputera Mac z Xcode i
odpowiedniego konta Apple. Obecny identyfikator aplikacji jest domyślnym
`com.companyname.barbarakierowca`: nie stanowi aktualizacji istniejącej
aplikacji `pl.tickettours.driver` i nie przenosi jej sesji ani kluczy. Przed
publikacją trzeba ustalić identyfikatory, podpisy i sposób migracji.

## Lokalizacja

Android używa usługi pierwszoplanowej z powiadomieniem. iOS korzysta z Core
Location; działanie w tle zależy od zgód i systemu. Na Windows lokalizacja jest
wysyłana tylko podczas działania aplikacji. Po ponownym otwarciu aktywnego
kursu użyj przycisku „Wznów GPS”. Wymuszone zatrzymanie aplikacji, brak
uprawnień, wyłączenie GPS, oszczędzanie baterii lub brak połączenia mogą
przerwać wysyłanie.

Przed użyciem produkcyjnym przetestuj na fizycznych urządzeniach logowanie,
listy i płatności, zgody, lokalizację w tle, utratę połączenia, wznowienie
oraz zakończenie kursu. Nie rozpoczynaj testu na prawdziwym kursie bez
uzgodnienia z operatorem.

Aktualna wersja API pozwala kontynuować aktywny kurs po ponownym zalogowaniu
tego samego kierowcy, pod warunkiem zachowania klucza kursu w aplikacji.
Wymaga to wdrożenia aktualnego pliku `driver-api.php` na serwerze.

## Wdrożenie API

Jeśli serwer zwraca HTTP 404 dla `/driver-api.php`, sprawdź adres i wdrożenie
API. Lokalny plik `F:\laragon\www\barbara\driver-api.php` należy umieścić w
katalogu głównym działającej instalacji Barbary. W jej bazie wymagane są
tabele z `F:\laragon\www\barbara\app\driver-schema.sql` oraz kolumny `gps`,
`gps_updated_at` i `gps_updated_by` w tabeli `kursy`. Sam plik SQL tworzy tylko
tabele, nie dodaje tych kolumn. Przed migracją sprawdź istniejącą strukturę i
wykonaj kopię bazy. Nie publikuj pliku SQL ani konfiguracji bazy w katalogu
dostępnym przez WWW.

Aktualna wersja API dopuszcza konta o randze `kierowca` oraz wskazane w
kodzie serwera konto superadministratora o randze `admin`.
