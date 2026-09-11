/**
 * Returns a relevant food emoji based on food name keywords.
 */
export function getFoodEmoji(foodName?: string): string {
  if (!foodName) return '🍽️';
  const name = foodName.toLowerCase().trim();

  // Meat & Poultry
  if (/chicken|turkey|poultry|wing|thigh|drumstick/.test(name)) return '🍗';
  if (/beef|steak|lamb|bison|venison|sirloin|ribeye|tenderloin|ground meat|patty|burger/.test(name)) return '🥩';
  if (/bacon|pork|ham|prosciutto|sausage/.test(name)) return '🥓';

  // Seafood
  if (/shrimp|prawn|crab|lobster/.test(name)) return '🦐';
  if (/salmon|tuna|fish|cod|tilapia|halibut|trout|sardine|anchovy|seafood/.test(name)) return '🐟';

  // Eggs & Dairy
  if (/egg|omelet|omelette|scramble/.test(name)) return '🥚';
  if (/cheese|feta|cheddar|mozzarella|parmesan|gouda|swiss|cottage/.test(name)) return '🧀';
  if (/milk|latte|creamer|dairy/.test(name)) return '🥛';
  if (/yogurt|skyr|curd/.test(name)) return '🥣';
  if (/butter|ghee|margarine/.test(name)) return '🧈';

  // Shakes & Supplements
  if (/whey|protein powder|protein shake|shake|smoothie|isolate|casein/.test(name)) return '🥤';

  // Grains & Bakery
  if (/bread|toast|sourdough|boule|bagel|bun|pita|tortilla|naan|roll|croissant|wrap/.test(name)) return '🍞';
  if (/rice|jasmine|basmati|quinoa|grain/.test(name)) return '🍚';
  if (/pasta|spaghetti|noodle|ramen|macaroni|penne|rigatoni|linguine/.test(name)) return '🍝';
  if (/oat|oats|oatmeal|porridge|cereal|granola|muesli/.test(name)) return '🥣';

  // Produce - Fruits
  if (/grape|grapes|raisin/.test(name)) return '🍇';
  if (/blueberry|blueberries|blackberry|blackberries|berry|berries/.test(name)) return '🫐';
  if (/strawberry|strawberries/.test(name)) return '🍓';
  if (/banana|bananas/.test(name)) return '🍌';
  if (/apple|apples/.test(name)) return '🍎';
  if (/orange|citrus|clementine|tangerine/.test(name)) return '🍊';
  if (/lemon|lime/.test(name)) return '🍋';
  if (/watermelon|melon|cantaloupe/.test(name)) return '🍉';
  if (/peach|apricot|plum/.test(name)) return '🍑';
  if (/pineapple/.test(name)) return '🍍';
  if (/mango|papaya/.test(name)) return '🥭';
  if (/cherry|cherries/.test(name)) return '🍒';
  if (/avocado|guac|guacamole/.test(name)) return '🥑';

  // Produce - Veggies
  if (/potato|potatoes|sweet potato|fries|yam/.test(name)) return '🥔';
  if (/broccoli|broccolini|cauliflower|asparagus/.test(name)) return '🥦';
  if (/salad|lettuce|spinach|kale|greens|arugula/.test(name)) return '🥗';
  if (/carrot|carrots/.test(name)) return '🥕';
  if (/corn|maize/.test(name)) return '🌽';
  if (/cucumber|pickle|pickles|zucchini/.test(name)) return '🥒';
  if (/tomato|tomatoes/.test(name)) return '🍅';
  if (/pepper|bell pepper|chili|jalapeno/.test(name)) return '🫑';
  if (/onion|garlic|shallot|leek/.test(name)) return '🧅';
  if (/mushroom|mushrooms/.test(name)) return '🍄';

  // Legumes, Dips & Fats
  if (/bean|beans|lentil|lentils|chickpea|chickpeas|edamame|legume/.test(name)) return '🫘';
  if (/hummus|falafel|tahini|dip/.test(name)) return '🧆';
  if (/olive oil|olive|olives/.test(name)) return '🫒';
  if (/peanut|peanut butter|almond|almonds|nut|nuts|walnut|cashew|pecan|pistachio/.test(name)) return '🥜';

  // Sweets & Condiments
  if (/jam|jelly|preserves|honey|syrup/.test(name)) return '🍯';
  if (/cookie|biscuit/.test(name)) return '🍪';
  if (/chocolate|cocoa|brownie/.test(name)) return '🍫';
  if (/pizza/.test(name)) return '🍕';
  if (/taco|burrito|quesadilla|fajita/.test(name)) return '🌮';
  if (/coffee|espresso|americano|cappuccino/.test(name)) return '☕';
  if (/tea|matcha/.test(name)) return '🍵';
  if (/water/.test(name)) return '💧';

  return '🍽️';
}
