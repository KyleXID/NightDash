# Specs - Stage 02~06 Enemy/Boss Numbers

이 문서는 Stage01에 이어 Stage02~06의 기준 수치를 정의한다.

## 공통
- 실제 값 = 기준 수치 * TimeScale * DifficultyModifier
- XP는 조정 대상이며 플레이테스트로 +-15% 범위에서 조절

## Stage 02: 부패한 숲

### 일반 몬스터
| ID | HP | ATK | SPD | XP |
|---|---:|---:|---:|---:|
| ENE_FOREST_FOLLOWER | 42 | 10 | 2.4 | 7 |
| ENE_FOREST_ARCHER | 36 | 12 | 2.2 | 8 |
| ENE_FOREST_VINEBEAST | 58 | 14 | 3.2 | 10 |
| ENE_FOREST_FUNGUS_GIANT | 170 | 18 | 1.7 | 18 |

### 엘리트/보스
| ID | HP | ATK | SPD | XP |
|---|---:|---:|---:|---:|
| ELT_FOREST_WARDEN | 560 | 28 | 2.3 | 80 |
| ELT_FOREST_TREANT | 620 | 30 | 1.9 | 90 |
| BOS_FOREST_VERDAN | 3600 | 36 | 2.7 | 450 |

## Stage 03: 그림자 늪

### 일반 몬스터
| ID | HP | ATK | SPD | XP |
|---|---:|---:|---:|---:|
| ENE_SWAMP_STALKER | 55 | 14 | 3.0 | 9 |
| ENE_SWAMP_THROWER | 48 | 15 | 2.4 | 10 |
| ENE_SWAMP_DEVOURER | 92 | 20 | 2.6 | 16 |

### 엘리트/보스
| ID | HP | ATK | SPD | XP |
|---|---:|---:|---:|---:|
| ELT_SWAMP_NAMELESS | 780 | 34 | 3.2 | 120 |
| BOS_SWAMP_MOR | 3000 | 34 | 3.4 | 380 |

## Stage 04: 타락한 성채

### 일반 몬스터
| ID | HP | ATK | SPD | XP |
|---|---:|---:|---:|---:|
| ENE_CITADEL_GUARD | 88 | 18 | 2.4 | 13 |
| ENE_CITADEL_SNIPER | 62 | 22 | 2.1 | 14 |
| ENE_CITADEL_BRUTE | 210 | 26 | 1.8 | 22 |
| ENE_CITADEL_SAPPER | 70 | 20 | 2.7 | 12 |

### 엘리트/보스
| ID | HP | ATK | SPD | XP |
|---|---:|---:|---:|---:|
| ELT_CITADEL_JUDGE | 980 | 40 | 2.5 | 150 |
| ELT_CITADEL_BISHOP | 1040 | 42 | 2.2 | 160 |
| BOS_CITADEL_HAKAN | 5200 | 45 | 2.8 | 650 |

## Stage 05: 절망의 설원

### 일반 몬스터
| ID | HP | ATK | SPD | XP |
|---|---:|---:|---:|---:|
| ENE_SNOW_WRAITH | 102 | 22 | 2.5 | 15 |
| ENE_SNOW_SPEARMAN | 86 | 24 | 2.3 | 16 |
| ENE_SNOW_GIANT | 280 | 32 | 1.7 | 28 |
| ENE_SNOW_CALLER | 92 | 23 | 2.0 | 17 |

### 엘리트/보스
| ID | HP | ATK | SPD | XP |
|---|---:|---:|---:|---:|
| ELT_SNOW_EXECUTIONER | 1320 | 48 | 2.6 | 190 |
| BOS_SNOW_CELIAS | 6800 | 52 | 2.9 | 820 |

## Stage 06: 마왕성

### 일반 몬스터
| ID | HP | ATK | SPD | XP |
|---|---:|---:|---:|---:|
| ENE_CASTLE_KNIGHT | 130 | 28 | 2.7 | 19 |
| ENE_CASTLE_MAGE | 108 | 30 | 2.3 | 20 |
| ENE_CASTLE_BEHEMOTH | 340 | 40 | 1.8 | 34 |
| ENE_CASTLE_ASSASSIN | 96 | 32 | 3.5 | 22 |

### 중간 보스/최종 보스
| ID | HP | ATK | SPD | XP |
|---|---:|---:|---:|---:|
| MID_CASTLE_RAK | 4200 | 46 | 3.0 | 300 |
| MID_CASTLE_ERN | 4600 | 44 | 2.6 | 320 |
| BOS_CASTLE_ASMODEL | 9800 | 62 | 3.1 | 1400 |

## 검증 목표
- Stage02 보스 도달율: 40~55%
- Stage03 클리어율: 50~65%
- Stage04 보스 도달율: 30~45%
- Stage05 보스 도달율: 20~35%
- Stage06 클리어율: 10~20%
