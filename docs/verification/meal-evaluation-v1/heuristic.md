# Meal evaluation: heuristic

Dataset 1.0.0; 66 attempted; stopped: no.

| Metric | Numerator / denominator | Rate |
|---|---|---|
| itemPrecision | 76 / 89 | 0.8539 |
| itemRecall | 76 / 86 | 0.8837 |
| fullyCorrectMeals | 41 / 66 | 0.6212 |
| explicitMassAccuracy | 52 / 61 | 0.8525 |
| portionAccuracy | 53 / 64 | 0.8281 |
| quantityAccuracy | 52 / 64 | 0.8125 |
| fallback | 66 / 66 | 1 |

Qualifier errors: 8; forbidden substitutions: 11; excluded-food violations: 1.

| Case | Category | Outcome | Violations |
|---|---|---|---|
| MASS-001 | mass | pass |  |
| MASS-002 | mass | pass |  |
| MASS-003 | mass | pass |  |
| MASS-004 | mass | pass |  |
| MASS-005 | mass | pass |  |
| MASS-006 | mass | pass |  |
| MASS-007 | mass | pass |  |
| MASS-008 | mass | pass |  |
| PREPARATION-001 | preparation | pass |  |
| PREPARATION-002 | preparation | pass |  |
| PREPARATION-003 | preparation | pass |  |
| PREPARATION-004 | preparation | pass |  |
| PREPARATION-005 | preparation | pass |  |
| PREPARATION-006 | preparation | pass |  |
| PREPARATION-007 | preparation | needs work | food-1: preparation/brand/diet/lean qualifier mismatch |
| PREPARATION-008 | preparation | needs work | food-1: preparation/brand/diet/lean qualifier mismatch |
| QUALIFIERS-001 | qualifiers | pass |  |
| QUALIFIERS-002 | qualifiers | pass |  |
| QUALIFIERS-003 | qualifiers | pass |  |
| QUALIFIERS-004 | qualifiers | pass |  |
| QUALIFIERS-005 | qualifiers | pass |  |
| QUALIFIERS-006 | qualifiers | pass |  |
| QUALIFIERS-007 | qualifiers | pass |  |
| QUALIFIERS-008 | qualifiers | pass |  |
| BRANDS-001 | brands | needs work | food-1: preparation/brand/diet/lean qualifier mismatch, resolution/source expectation differs |
| BRANDS-002 | brands | needs work | food-1: preparation/brand/diet/lean qualifier mismatch, resolution/source expectation differs |
| BRANDS-003 | brands | needs work | food-1: missing food; extra foods |
| BRANDS-004 | brands | pass |  |
| BRANDS-005 | brands | needs work | food-1: missing food; extra foods |
| BRANDS-006 | brands | needs work | food-1: preparation/brand/diet/lean qualifier mismatch, resolution/source expectation differs |
| BRANDS-007 | brands | needs work | food-1: preparation/brand/diet/lean qualifier mismatch, resolution/source expectation differs |
| BRANDS-008 | brands | pass |  |
| VAGUE-001 | vague | pass |  |
| VAGUE-002 | vague | pass |  |
| VAGUE-003 | vague | pass |  |
| VAGUE-004 | vague | pass |  |
| VAGUE-005 | vague | pass |  |
| VAGUE-006 | vague | pass |  |
| VAGUE-007 | vague | pass |  |
| VAGUE-008 | vague | pass |  |
| MIXED-001 | mixed | needs work | food-1: missing food; extra foods |
| MIXED-002 | mixed | needs work | food-1: missing food; extra foods |
| MIXED-003 | mixed | needs work | food-1: missing food; extra foods |
| MIXED-004 | mixed | needs work | food-1: missing food; extra foods |
| MIXED-005 | mixed | needs work | food-1: quantity/unit differ |
| MIXED-006 | mixed | needs work | food-1: portion grams differ, quantity/unit differ, resolution/source expectation differs, reference nutrition outside fixture tolerances |
| MIXED-007 | mixed | needs work | food-1: quantity/unit differ |
| MIXED-008 | mixed | needs work | food-1: missing food; extra foods |
| CORRECTIONS-001 | corrections | needs work | food-1: portion grams differ, quantity/unit differ, reference nutrition outside fixture tolerances; extra foods |
| CORRECTIONS-002 | corrections | pass |  |
| CORRECTIONS-003 | corrections | pass |  |
| CORRECTIONS-004 | corrections | pass |  |
| CORRECTIONS-005 | corrections | needs work | ; extra foods; excluded foods returned |
| CORRECTIONS-006 | corrections | needs work | ; extra foods |
| CORRECTIONS-007 | corrections | pass |  |
| CORRECTIONS-008 | corrections | needs work | food-1: portion grams differ, reference nutrition outside fixture tolerances; food-2: portion grams differ, reference nutrition outside fixture tolerances |
| UNKNOWN-001 | unknown | needs work | food-1: missing food; extra foods |
| UNKNOWN-002 | unknown | pass |  |
| UNKNOWN-003 | unknown | pass |  |
| UNKNOWN-004 | unknown | pass |  |
| UNKNOWN-005 | unknown | pass |  |
| UNKNOWN-006 | unknown | pass |  |
| UNKNOWN-007 | unknown | needs work | food-1: missing food; extra foods |
| UNKNOWN-008 | unknown | needs work | food-1: missing food; extra foods |
| FULL-DAY-001 | full-day | needs work | food-2: portion grams differ, quantity/unit differ, reference nutrition outside fixture tolerances; food-3: quantity/unit differ |
| FULL-DAY-002 | full-day | needs work | food-2: preparation/brand/diet/lean qualifier mismatch, resolution/source expectation differs; food-4: preparation/brand/diet/lean qualifier mismatch; food-5: resolution/source expectation differs, uncertainty is not visible |

Rates are identity/constraint checks on synthetic prompts, not independent nutrition or live-model accuracy. See the JSON report for version hashes, execution context, actual foods, category metrics, unknown/estimated outcomes, and live request accounting.
