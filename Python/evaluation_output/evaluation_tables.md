# Rezultate evaluare model

## Tabel 1 — Configuratia modelului antrenat
| Parametru | Valoare |
|---|---|
| Arhitectura de baza | efficientnet_v2_s |
| Dimensiune imagine | 224 x 224 |
| Batch size | 32 |
| Numar de epoci | 12 |
| Epoci cu backbone inghetat | 2 |
| Learning rate (cap) | 0.000900 |
| Learning rate (fine-tune) | 0.000180 |
| Label smoothing | - |
| Mixed precision (AMP) | True |
| Seed | 42 |
| Total imagini | 10430 |
| Imagini antrenare | 9178 |
| Imagini validare | 1252 |

## Tabel 2 — Metrici globale pe setul de validare (threshold = 0.5)
| Metrica | Valoare |
|---|---|
| Acuratete | 1.0000 |
| Acuratete balansata | 1.0000 |
| Precizie | 1.0000 |
| Recall (sensibilitate) | 1.0000 |
| Specificitate | 1.0000 |
| F1-Score | 1.0000 |
| AUC (ROC) | 1.0000 |
| MCC (Matthews) | 1.0000 |
| Kappa Cohen | 1.0000 |

## Tabel 3 — Matricea de confuzie
|  | Prezis: BENIGN | Prezis: MALIGNANT | Total |
|---|---|---|---|
| Real: BENIGN | 1047 | 0 | 1047 |
| Real: MALIGNANT | 0 | 205 | 205 |
| Total | 1047 | 205 | 1252 |

## Tabel 4 — Metrici per clasa
| Clasa | Precizie | Recall | F1 | Support |
|---|---|---|---|---|
| BENIGN | 1.0000 | 1.0000 | 1.0000 | 1047 |
| MALIGNANT | 1.0000 | 1.0000 | 1.0000 | 205 |

## Tabel 5 — Variatia metricilor in functie de pragul de decizie
| Prag | Acuratete | Precizie | Recall | Specificitate | F1 |
|---|---|---|---|---|---|
| 0.1 | 0.9984 | 0.9903 | 1.0000 | 0.9981 | 0.9951 |
| 0.2 | 0.9992 | 0.9951 | 1.0000 | 0.9990 | 0.9976 |
| 0.3 | 0.9992 | 0.9951 | 1.0000 | 0.9990 | 0.9976 |
| 0.4 | 0.9992 | 0.9951 | 1.0000 | 0.9990 | 0.9976 |
| 0.5 | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 1.0000 |
| 0.6 | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 1.0000 |
| 0.7 | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 1.0000 |
| 0.8 | 0.9992 | 1.0000 | 0.9951 | 1.0000 | 0.9976 |
| 0.9 | 0.9992 | 1.0000 | 0.9951 | 1.0000 | 0.9976 |

## Tabel 6 — Distributia probabilitatilor prezise
| Interval probabilitate | BENIGN | MALIGNANT | Total |
|---|---|---|---|
| [0.0 - 0.1) | 1045 | 0 | 1045 |
| [0.1 - 0.2) | 1 | 0 | 1 |
| [0.2 - 0.3) | 0 | 0 | 0 |
| [0.3 - 0.4) | 0 | 0 | 0 |
| [0.4 - 0.5) | 1 | 0 | 1 |
| [0.5 - 0.6) | 0 | 0 | 0 |
| [0.6 - 0.7) | 0 | 0 | 0 |
| [0.7 - 0.8) | 0 | 1 | 1 |
| [0.8 - 0.9) | 0 | 0 | 0 |
| [0.9 - 1.0] | 0 | 204 | 204 |

## Tabel 7 — Performanta inferentei
| Indicator | Valoare |
|---|---|
| Latenta medie per imagine (ms) | 4.88 |
| Latenta mediana (ms) | 1.50 |
| Latenta minima (ms) | 1.36 |
| Latenta maxima (ms) | 320.56 |
| Throughput (imagini/sec) | 205.12 |
| Numar total parametri | 20,180,050 |
| Parametri antrenabili | 20,180,050 |
| Dimensiune fisier model (MB) | 77.80 |
| Device utilizat | cuda |

## Tabel 8 — Top 10 cele mai incerte predictii
| # | Fisier | Eticheta reala | Prezis | P(MALIGNANT) | Corect |
|---|---|---|---|---|---|
| 1 | IMG (173)_LinearContrast.jpg | BENIGN | BENIGN | 0.4671 | DA |
| 2 | IMG (67)_AdditiveGaussianNoise.jpg | MALIGNANT | MALIGNANT | 0.7204 | DA |
| 3 | IMG (54)_LinearContrast.jpg | BENIGN | BENIGN | 0.1368 | DA |
| 4 | IMG (440)_LinearContrast.jpg | BENIGN | BENIGN | 0.0837 | DA |
| 5 | IMG (94)_AdditiveGaussianNoise.jpg | MALIGNANT | MALIGNANT | 0.9216 | DA |
| 6 | IMG (43)_PerspectiveTransform.jpg | BENIGN | BENIGN | 0.0504 | DA |
| 7 | IMG (601)_LinearContrast.jpg | BENIGN | BENIGN | 0.0495 | DA |
| 8 | IMG (92)_LinearContrast.jpg | MALIGNANT | MALIGNANT | 0.9652 | DA |
| 9 | IMG (48)_Dropout.jpg | BENIGN | BENIGN | 0.0228 | DA |
| 10 | IMG (38)_Rotate.jpg | BENIGN | BENIGN | 0.0227 | DA |

## Tabel 9 — Comparatie cu literatura
| Study | Dataset | Architecture | Accuracy | F1 | AUC |
|---|---|---|---|---|---|
| RadiNet-XGBoost | Mammogram Mastery | CNN + XGBoost | 0.966 | - | - |
| MedFoundX | Mammogram Mastery | Deep Learning | 0.987 | - | - |
| TT-Stack Ensemble | Mammogram Mastery | Vision Transformers | 0.993 | 0.979 | 0.999 |
| Current Work | Mammogram Mastery | efficientnet_v2_s | 1.0000 | 1.0000 | 1.0000 |
