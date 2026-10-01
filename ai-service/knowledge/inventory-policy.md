# Inventory Policy

Owner: Inventory Manager.

## Reorder points and safety stock

Every product has a reorder point and a safety stock level. Safety stock is the buffer that protects against demand
spikes and late deliveries; it can never exceed the reorder point.

When available stock (on hand minus quantities reserved for confirmed orders) falls to or below the reorder point, a
replenishment purchase order should be raised the same week.

Falling below safety stock is treated as urgent: the Inventory Manager is notified and replenishment is expedited.

## Order quantities

Replenishment quantities should bring stock back to the reorder point plus expected demand during the supplier's
lead time, net of quantities already on order. Quantities are rounded up to multiples of 10 units.

## Stock adjustments

Cycle counts are performed monthly for every warehouse. Differences are recorded as count adjustments with a note.
Damaged goods are written off as damaged stock and must include the reason. Adjustments over 100 units or $5,000 in
value require Inventory Manager review.

## Transfers

Stock may be transferred between warehouses to cover local shortages. Reserved stock cannot be transferred.
