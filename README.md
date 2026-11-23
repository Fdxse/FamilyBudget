# FamilyBudget

Familjebudget för fakturor, inkomster och större utgifter, med månadsbudget och årsöversikt.
Byggd i C# / ASP.NET Core (.NET 10, Blazor Server) med MariaDB, och körs i Docker på en Synology NAS.

## Projektstruktur

| Sökväg | Innehåll |
| --- | --- |
| `src/FamilyBudget.Web` | Webbappen (Blazor Server + EF Core) |
| `Dockerfile` | Bygger app-imagen |
| `docker-compose.yml` | App + MariaDB, data på NAS-sharen |
| `.env.example` | Mall för inställningar och lösenord (kopieras till `.env` på NAS:en) |
| `deploy/update.sh` | Körs på NAS:en: hämtar från GitHub, bygger, startar om, väntar på `/health` |
| `deploy/remote-update.ps1` / `.sh` | Körs från din dator: kör `update.sh` på NAS:en via SSH |

## Första uppsättningen på NAS:en (DS918+, DSM 7.2)

1. **Paket i Package Center:** installera *Container Manager* och *Git Server* (det senare ger kommandot `git`).
2. **SSH:** Kontrollpanelen → Terminal och SNMP → aktivera SSH. Din användare måste vara administratör.
3. **Delad mapp:** allt ligger i den delade mappen `Ekonomi` (`/volume1/Ekonomi`), med denna struktur:
   ```
   /volume1/Ekonomi/FamilyBudget/
   ├── app/    # klon av GitHub-repot, skrivs över vid varje uppdatering
   └── data/   # databasfiler (mariadb/) och inloggningsnycklar (keys/), ta med i backup
   ```
   Datamappen skapas automatiskt av `update.sh`.
4. **Åtkomst till GitHub från NAS:en** (behövs om repot är privat). Logga in med SSH och skapa en nyckel:
   ```bash
   ssh-keygen -t ed25519 -f ~/.ssh/familybudget_deploy -N ""
   cat ~/.ssh/familybudget_deploy.pub
   ```
   Lägg in den publika nyckeln på GitHub: repot → Settings → Deploy keys → Add deploy key (bara läsrätt).
   Lägg sedan till i `~/.ssh/config` på NAS:en:
   ```
   Host github.com
       IdentityFile ~/.ssh/familybudget_deploy
   ```
5. **Klona repot:**
   ```bash
   mkdir -p /volume1/Ekonomi/FamilyBudget && cd /volume1/Ekonomi/FamilyBudget
   git clone git@github.com:Fdxse/FamilyBudget.git app
   cd app
   cp .env.example .env
   vi .env    # sätt DATA_ROOT, lösenord och DEPLOY_BRANCH
   ```
6. **Första starten:**
   ```bash
   ./deploy/update.sh
   ```
   Öppna `http://<nas>:5080`. Första gången kommer du till sidan **Första uppstarten** där du skapar
   ditt administratörskonto (och gärna din första budgetgrupp). Sidan stängs så fort ett konto finns.
7. **(Valfritt) HTTPS:** Kontrollpanelen → Inloggningsportal → Avancerat → Omvänd proxy.
   Peka t.ex. `https://budget.dindomän.se` mot `http://localhost:5080` och lägg till anpassade rubriker för WebSocket
   (knappen *Skapa → WebSocket*), annars fungerar inte Blazor.

## Uppdatera från din dator

Från Windows (PowerShell):

```powershell
.\deploy\remote-update.ps1 -NasSshTarget fredrik@192.168.1.10
```

Från Git Bash / Linux / macOS:

```bash
./deploy/remote-update.sh fredrik@192.168.1.10
```

Skripten utgår från att repot ligger i `/volume1/Ekonomi/FamilyBudget/app`. Ligger det någon annanstans anger du
`-NasRepositoryPath` (PowerShell) eller ett andra argument (bash).

Skriptet loggar in på NAS:en, hämtar senaste koden från GitHub, bygger en ny image, startar om containrarna
och väntar tills `/health` svarar `Healthy`. Om något går fel skrivs de sista loggraderna ut.

**Slippa lösenord:**
- Lägg din publika SSH-nyckel i `~/.ssh/authorized_keys` på NAS:en (aktivera *Användarhemtjänst* i DSM först).
- `sudo` frågar efter lösenord varje gång. Vill du slippa det kan du som root lägga till en rad i
  `/etc/sudoers.d/familybudget`: `fredrik ALL=(ALL) NOPASSWD: /usr/local/bin/docker, /bin/mkdir, /bin/chown`
  (kontrollera sökvägen med `which docker`).

## Köra lokalt (utveckling)

Kräver .NET 10 SDK och en MariaDB. Ändra anslutningssträngen i `src/FamilyBudget.Web/appsettings.Development.json`
eller sätt den som user secret, och kör:

```bash
dotnet run --project src/FamilyBudget.Web
```

## Användare och budgetgrupper

- **Administratörer** lägger upp konton under *Användare* och grupper under *Budgetgrupper*.
- En **budgetgrupp** är en egen budget, t.ex. "Fredrik och Anna" eller "Kalle". Allt som registreras
  (fakturor, inkomster) hör till en grupp, och bara gruppens medlemmar ser det.
- En person kan vara med i flera grupper. Den som glömt sitt lösenord får ett nytt av en administratör,
  och kan sedan byta det själv under *Byt lösenord*. Efter 5 felaktiga försök låses kontot i 5 minuter.

## Fakturor och inkomster

Klicka på en budgetgrupp på startsidan. Där finns fyra flikar:

- **Månad:** allt som förfaller under månaden, med summor för inkomster, utgifter och vad som blir över.
  Markera fakturor som *Betald* och inkomster som *Mottagen*. Beloppet kan justeras först (t.ex. lön med övertid).
- **Fakturor och inkomster:** alla poster med periodicitet (engång, månad, kvartal, halvår, år),
  nästa förfallodag och snittkostnad per månad.
- **Mallar:** färdiga utgångspunkter, t.ex. "Lön" med vanligt belopp och dag. *Lägg till* öppnar en ifylld ny post.
- **Kategorier:** varje grupp har egna kategorier. Standardkategorier skapas första gången gruppen öppnas.

## År, budget och statistik

- **År**: inkomster, utgifter och netto per månad med graf, och en tabell per kategori och månad.
  Betalda poster räknas med betalt belopp, obetalda med förväntat belopp.
- **Budget**: sätt en månadsbudget per kategori och jämför med utfallet för en vald månad.
  Kolumnen "Snitt 12 mån" hjälper till att välja en rimlig nivå. Årsvyn markerar månader över budget med rött.
- **Statistik**: år mot år, fördelning av utgifter per kategori för ett valt år och årets största utgifter.
  Under **Per namn** summeras allt med samma namn på en rad. Klicka dig ner från hela perioden till år och månad,
  eller klicka på ett namn för att se det över tiden.

## Databasändringar (migreringar)

Tabellerna skapas och uppdateras automatiskt när appen startar. Vid ändringar i datamodellen skapas en ny
migrering lokalt:

```bash
dotnet tool restore
dotnet ef migrations add <Namn> --project src/FamilyBudget.Web --output-dir Data/Migrations
```

## Paket

- `Pomelo.EntityFrameworkCore.MySql` 9.0.0 (EF Core-drivrutin för MariaDB/MySQL)
- `MySqlConnector` 2.6.2 (uppdaterad för att täppa till en känd sårbarhet i 2.4.0)
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 9.0.23 (inloggning och användare)
- `Microsoft.EntityFrameworkCore.Design` 9.0.23 och verktyget `dotnet-ef` 9.0.23 (bara för att skapa migreringar)
- Chart.js 4.6.1 (MIT, grafer). Filen ligger i `wwwroot/lib/chartjs` så appen inte behöver internet.

Pomelo finns ännu inte i en version för EF Core 10, så appen använder EF Core 9 ovanpå .NET 10. Det fungerar fullt ut;
vi byter till Pomelo 10 när den släpps.
