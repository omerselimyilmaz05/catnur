# CatNur — Test ve Davranış Doğrulama Kontrol Listesi (TEST_CHECKLIST.md)

Bu dosya, projede geliştirilen, çalışan ve korunması gereken **tüm temel davranışları ve kuralları** listeler.
> [!IMPORTANT]
> **KURAL:** Bu dosya kullanıcının açık talimatı olmadan **kesinlikle değiştirilemez veya yeni keyfi kurallar eklenemez**. Her değişiklik sonrası bu listedeki tüm testler eksiksiz çalıştırılmalı ve doğrulanmalıdır.

---

## 1. Mouse Etkileşimi, Sürükleme ve Fırlatma (Drag & Throw)
- [ ] **1:1 DPI Mouse Takibi:** Windows DPI ölçeği (%100, %125, %150 vb.) ne olursa olsun, kedi tıklandığı ve sürüklendiği noktayı mouse imleciyle 1:1 birebir takip eder; kayma/drift yapmaz.
- [ ] **Fırlatma / Bırakma Ayrımı:**
  - Kedi mouse ile hızlıca savrulup bırakıldığında (`hız >= 110 px/s`), son hareket ivmesiyle ekran boyunca fırlatılır (`ThrowDamping = 0.88`, `MaxThrowSpeed = 1500 px/s`).
  - Kedi havada hareketsiz tutulup bırakıldığında (`hız < 110 px/s`) fırlatılmaz, olduğu yerden dikey olarak doğal şekilde düşer (`Falling`).
- [ ] **Sürükleme Pozu:** Fareyle tutulup taşınırken `tutulma` görselinde (enseden asılı kalma) sabit kalır.
- [ ] **Tek Tıklama:** Kediye tek tıklandığında (300ms çift tık bekleme süresi sonrası) ağzını açıp miyavlar (`miyavlama` 3 kare), 5 rastgele miyavlama sesinden birini çalar (%20 ses) ve minik bir yaylanma efekti yapar.
- [ ] **Çift Tıklama:** Kediye 300ms içinde art arda 2 kez tıklandığında miyavlama hiç başlamadan doğrudan 2 saniye boyunca sırtını kabartıp tıslar (`tisslama` 2 kare) ve kısık tıslama sesini çalar (%15 ses).
- [ ] **Sağ Tıklama / Yatağa Yürüme ve Uyuma:** Kediye sağ tıklandığında olduğu yerde kayarak gitmez; önce yatağına/köşesine doğru yürür (`yurume`), yerine ulaştığında kıvrılıp uykuya dalar (`uyuma_v1/v3/v4` + döngüde `uyuma.mp3`). Sol tıklandığında uyanır.

---

## 2. Fizik Motoru, Yerçekimi ve Düşüş (Physics & Gravity)
- [ ] **Yerçekimi ve Düşüş Hızı:** Yerçekimi ivmesi ($2400\text{ px/s}^2$) ve terminal düşüş hızı ($1800\text{ px/s}$) ile kedi ağırlığını hissettirerek düşer.
- [ ] **Sekme ve Zemine Oturma:** Yere veya platforma inişte tek bir küçük sıçrama/sekme yapar ($0.35$ sekme katsayısı) ve ardından zemine pürüzsüzce oturur (`IsGrounded = true`).
- [ ] **Işınlanma Engeli:** Kedi bir platformun altından geçerken veya platform altındayken yukarıya doğru ışınlanmaz; çarpışma yalnızca yukarıdan aşağıya inerken (`previousBottom <= Top`) gerçekleşir.
- [ ] **Gökyüzü Portalı (Screen Wrap / Sky Drop):** Kedi görev çubuğunun altına çekilip bırakıldığında veya aşağıya doğru fırlatıldığında ekrandan tamamen kaybolup gitmez; aşağı düştüğü X koordinatının tam üstünden gökyüzünden (ekranın tepesinden) aşağı doğru süzülerek düşer.
- [ ] **Duvardan Kayma (`duvardan_kayma` - Satır 3 Sütun 4 - 90 Derece Sola Döndürülmüş):** Kedi ekranın sol veya sağ dış sınırlarına çarparak düşerken patilerini duvara geçirir ve düşüş hızı yavaşlayarak ($150\text{ px/s}$) tırnaklarıyla duvardan aşağı süzülerek kayar. Kuyruğu pencere çerçevesi tarafından kesilmez (`Margin = 0`).

---

## 3. Pencere Platformu, HWND ve Z-Order Takibi (Window Platforms)
- [ ] **HWND Platform Sahipliği:** Kedi bir pencerenin üzerine konduğunda o pencerenin handle'ını (`StandingHWnd`) saklar; masaüstüne veya başka pencerelere tıklansa dahi kedi durduğu pencerede kalır.
- [ ] **Gerçekçi Platform İvmesi (Trambolin Fırlatma & Boşluğa Düşüş):**
  - **Hızlı Yukarı Fırlatma (< -350 px/s):** Pencere aniden hızla yukarı çekilip bırakıldığında kedi platformun ivmesiyle havaya doğru fırlar, tepe noktasına ulaşıp geri iner.
  - **Hızlı Aşağı Çekme (> +400 px/s):** Pencere yerçekiminden daha hızlı şekilde aşağıya kaçırıldığında altındaki platform boşa çıkar; kedi havada asılı kalmayıp yerçekimiyle doğal düşüşe (`Falling`) geçer.
  - **Normal / Dengeli Taşıma:** Makul hızlarda sağa/sola/yukarı taşınırken pencereye 1:1 yapışık kayar.
- [ ] **Z-Order ve Tam Ekran Örtme:** Kedinin durduğu pencerenin üzerine başka bir pencere (veya tam ekran Discord/Tarayıcı) geldiğinde kedi görünmeyen pencerede havada asılı kalmaz, doğal şekilde aşağı düşer.
- [ ] **Pencere Kapanma / Küçülme:** Altındaki pencere kapatıldığında veya simge durumuna küçültüldüğünde kedi anında yerçekimiyle düşmeye başlar.

---

## 4. Otonom Gezinme ve Platform Güvenliği (Wander & Idle)
- [ ] **Pencereye İlk Konmada 15 Saniyelik Güvenli Kalış:** Kedi bir pencereye ilk zıpladığında veya konduğunda ilk 15 saniye boyunca pencere kenarlarına gelince geri döner (hemen aşağı düşmez, keyifle gezinir). 15 saniye içinde başka bir pencereye zıplayabilir; 15 saniye geçtikten sonra kenardan doğal olarak aşağı düşebilir.
- [ ] **Pencere Kenarından Doğal Düşüş (Ağırlık Merkezi / Patiler):** 15 saniye dolduktan sonra platform desteği patilere (`Left + 35px .. Right - 35px`) bağlıdır. Kedi platformun ucuna geldiğinde patileri boşa çıktığı an havada asılı kalmadan derhal aşağı düşer.
- [ ] **Sprite Yön Aynalama:** Kedi sağa giderken sağa, sola giderken sola bakar (moonwalk/ters yürüme hatası yoktur).

---

## 5. Otonom ve Yeni Pencere Zıplama Sistemi (Autonomous Jumping)
- [ ] **Yeni Pencere Açıldığında Anında Atlama:** Yeni bir pencere/klasör açıldığında kedi o an zemin üzerinde veya başka bir pencerede olsa bile **%90 yüksek ihtimalle** hemen yeni pencereye doğru balistik kavisle ($V_x$ ve $V_y$) sıçrar.
- [ ] **Klasörden Diğer Klasöre Atlama:** İki veya daha fazla klasör/pencere açıkken, kedi durduğu klasörün üzerindeyken **~4.5 - 6 saniyede bir %45 ihtimalle** diğer açık klasöre doğru balistik kavisle atlar.
- [ ] **Olduğu Yerde Zıplamama Garantisi:** Kedi tek bir klasörün üzerindeyken veya zemindeyken durduk yere olduğu yerde yukarı zıplamaz. Kendi durduğu pencere hedef listesinden kesinlikle hariç tutulur.
- [ ] **Kalkış Dokunulmazlığı ($0.6\text{s}$):** Havalandığı anda kalkış yaptığı platform $0.6$ saniye boyunca çarpışma testinden muaf tutulur; kedi eski platformuna takılmadan diğer pencereye doğru uçar.
- [ ] **Görünmez/Hayalet Pencereleri Eleme:** `DWMWA_CLOAKED` ve `WS_EX_TOOLWINDOW` filtreleri ile arka plan sistem servisleri (Arama, Ayarlar, Widget'lar vb.) kesinlikle zıplama hedefi yapılmaz; kedi boşluğa atlamaz.

---

## 6. Iskalama ve Toparlanma (Recovery & Landing)
- [ ] **Iskalama & Dört Ayak Üstüne Düşüş:** Kedi zıpladığında pencereye ulaşamayıp tabana (ScreenBottom) düşerse uykuya geçmez; dört ayak üstüne inip **0.5 saniye** `oturma` pozunda bekledikten sonra sakin gezinmeye devam eder.
- [ ] **Başarı Durumunda Tek Seferde Durma:** Kedi pencerenin üzerine başarıyla konduğunda bu kesin başarı sayılır; gereksiz ikinci bir zıplama yapmaz, pencerede `Idle` veya `Wander` durumuna geçer.

---

## 7. Müzik & Medya Dinleme (Catjam & Dance)
- [ ] **Spotify / YouTube / Medya Algılama:** Windows Media Session üzerinden müzik başladığında kedi otomatik olarak `muzik_dans` (6 kare ritmik dans) animasyonuna ve ritmik kafa sallama (`CatjamBehavior`) moduna geçer.
- [ ] **Müzik Durduğunda:** Müzik kapatıldığında veya duraklatıldığında kafa sallama açısı pürüzsüzce sıfırlanır ve normal gezinmeye döner.

---

## 8. Özel Etkileşimler ve Mini Davranışlar (Special Behaviors)
- [ ] **Fare İmleci Kovalama (Mouse Chase):** Fare kedinin yakınında hızlıca gezdirildiğinde kedi meraklanıp fareyi kovalamaya başlar (`MouseChaseBehavior`).
- [ ] **Su İçme Hatırlatıcısı (45 Dakika):** Her 45 dakikada bir kedinin üzerinde tek bir *"💧 Su içmeyi unutma!"* bildirim balonu gösterir (`BreakReminderService`).
- [ ] **Gece Modu / Uyku Döngüsü:** Gece saatlerinde (02:00 - 06:00) kedi daha uykulu davranır, daha sık `SleepingState`'e geçer (`TimeBehaviorService`).

---

## 9. Görsel, Animasyon ve Ses Varlık Denetimi (Visuals & Audio)
- [ ] **20 Aktif Türkçe Animasyon Klasörü:** `dusme`, `duvardan_kayma`, `miyavlama`, `muzik_dans`, `oturma`, `otururken_esneme`, `otururken_pati_yalama`, `sag_kulak_kasima`, `sol_kulak_kasima`, `tisslama`, `tutulma`, `uyuma_v1`, `uyuma_v3`, `uyuma_v4`, `uyurken_el_yalama_v1`, `uyurken_esneme_v1`, `yer_eseleme`, `yurume`, `ziplama`, `ziplama_hazirlik`.
- [ ] **7 Ses Dosyası:** `miyav_1.mp3` .. `miyav_5.mp3` (%20 ses), `tisslama.mp3` (%15 ses), `uyuma.mp3` (döngü).
- [ ] **Bağımsız Release Paketi (`D:\Yazılım\CatNur\CatNur\`):** `CatNur.exe` tek başına tüm bağımlılıklarıyla çalışır; masaüstüne çıkarılıp kullanılabilir.
- [ ] **Windows Başlangıcında Otomatik Çalışma:** Registry'e (`HKCU\...\Run`) kendini otomatik senkronize eder; çift örneği engellemek için `Local\CatNur_SingleInstance_App_Mutex` kullanır.
